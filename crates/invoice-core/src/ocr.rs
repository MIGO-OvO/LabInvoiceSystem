use crate::{importing::normalize_date, model::*, security::*};
use anyhow::{ensure, Context, Result};
use base64::Engine;
use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct OcrFields {
    pub purchase_date: String,
    pub amount_cents: i64,
    pub item_name: String,
    pub invoice_number: String,
    pub seller_name: String,
    pub seller_tax_id: String,
}
pub const OCR_REVIEW_WARNING: &str = "云 OCR 已完成，请核对所有字段与分类";

pub fn has_structured_metadata(invoice: &Invoice) -> bool {
    invoice.sources.iter().any(|source| {
        matches!(
            source.metadata_kind.as_str(),
            "manifest-v1" | "legacy-json" | "legacy-excel" | "legacy-migration" | "manual-excel"
        )
    })
}

pub fn needs_automatic_ocr(invoice: &Invoice) -> bool {
    invoice.state == "draft"
        && !invoice.reviewed
        && invoice.duplicate_of.is_none()
        && !invoice
            .warnings
            .iter()
            .any(|warning| warning == OCR_REVIEW_WARNING)
        && !(has_structured_metadata(invoice) && invoice.validate().is_ok())
}

pub fn apply_fields(
    invoice: &mut Invoice,
    fields: OcrFields,
    settings: &Settings,
    automatic: bool,
) {
    // Partial structured metadata is authoritative too. Automatic OCR fills its gaps.
    let fill_only = automatic && has_structured_metadata(invoice);
    if !fill_only || invoice.purchase_date.is_empty() {
        invoice.purchase_date = fields.purchase_date;
    }
    if !fill_only || invoice.amount_cents == 0 {
        invoice.amount_cents = fields.amount_cents;
    }
    if !fill_only || invoice.invoice_number.is_empty() {
        invoice.invoice_number = fields.invoice_number;
    }
    if !fill_only || invoice.seller_name.is_empty() {
        invoice.seller_name = fields.seller_name;
    }
    if !fill_only || invoice.seller_tax_id.is_empty() {
        invoice.seller_tax_id = fields.seller_tax_id;
    }
    let seller = if invoice.seller_tax_id.is_empty() {
        &invoice.seller_name
    } else {
        &invoice.seller_tax_id
    };
    let key = format!(
        "{}\u{1f}{}",
        seller.trim().to_uppercase(),
        fields.item_name.trim().to_uppercase()
    );
    let corrected = settings
        .item_corrections
        .get(&key)
        .cloned()
        .unwrap_or(fields.item_name.clone());
    if !fill_only || invoice.item_name.is_empty() {
        invoice.item_name = corrected;
    }
    invoice.ocr_item_name = fields.item_name;
    invoice.reviewed = false;
    if !invoice
        .warnings
        .iter()
        .any(|warning| warning == OCR_REVIEW_WARNING)
    {
        invoice.warnings.push(OCR_REVIEW_WARNING.into());
    }
}
fn words(value: &Value) -> Vec<String> {
    match value {
        Value::String(s) => vec![s.clone()],
        Value::Object(map) => map.get("word").map(words).unwrap_or_default(),
        Value::Array(items) => items.iter().flat_map(words).collect(),
        _ => vec![],
    }
}
fn meaningful(value: &str) -> bool {
    let value = value.trim();
    if value.is_empty() {
        return false;
    }
    fn numeric(value: &str) -> bool {
        !value.is_empty()
            && (value.parse::<f64>().is_ok()
                || value
                    .chars()
                    .all(|c| "零〇一二三四五六七八九十百千万两".contains(c)))
    }
    if numeric(value) {
        return false;
    }
    for unit in [
        "毫米", "厘米", "千克", "公斤", "毫升", "mm", "cm", "kg", "ml", "个", "件", "只", "套",
        "箱", "包", "支", "张", "台", "瓶", "盒", "组", "枚", "卷", "本", "块", "根", "条", "辆",
        "对", "双", "片", "份", "米", "克", "升", "m", "g", "l",
    ] {
        let lower = value.to_lowercase();
        if let Some(prefix) = lower.strip_suffix(unit) {
            if prefix.trim().is_empty() || numeric(prefix.trim()) {
                return false;
            }
        }
    }
    true
}
fn clean(value: &str) -> String {
    value
        .chars()
        .filter(|c| !"*#&-_".contains(*c))
        .collect::<String>()
        .trim()
        .to_owned()
}

pub async fn test_connection(api_key: &str, secret_key: &str) -> Result<()> {
    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(20))
        .redirect(reqwest::redirect::Policy::none())
        .build()?;
    let response = client
        .post("https://aip.baidubce.com/oauth/2.0/token")
        .form(&[
            ("grant_type", "client_credentials"),
            ("client_id", api_key),
            ("client_secret", secret_key),
        ])
        .send()
        .await
        .map_err(|_| anyhow::anyhow!("百度连接失败"))?
        .json::<Value>()
        .await?;
    ensure!(
        response
            .get("access_token")
            .and_then(|v| v.as_str())
            .is_some(),
        "百度凭据校验未成功"
    );
    Ok(())
}

pub fn parse_response(response: &Value) -> Result<OcrFields> {
    ensure!(
        response.get("error_code").is_none(),
        "百度 OCR 返回错误代码 {}，请检查凭据、额度或稍后重试",
        response.get("error_code").unwrap_or(&Value::Null)
    );
    let result = response
        .get("words_result")
        .filter(|v| v.is_object())
        .context("OCR 返回缺少 words_result")?;
    let get = |key: &str| {
        result
            .get(key)
            .map(words)
            .and_then(|v| v.into_iter().find(|v| !v.trim().is_empty()))
            .unwrap_or_default()
    };
    let names = result
        .get("CommodityName")
        .map(words)
        .unwrap_or_default()
        .into_iter()
        .map(|v| clean(&v))
        .collect::<Vec<_>>();
    let specs = result
        .get("CommodityType")
        .map(words)
        .unwrap_or_default()
        .into_iter()
        .map(|v| clean(&v))
        .collect::<Vec<_>>();
    let mut items = Vec::new();
    for index in 0..names.len().max(specs.len()) {
        let spec = specs.get(index).map(|v| v.as_str()).unwrap_or("");
        let name = names.get(index).map(|v| v.as_str()).unwrap_or("");
        let item = if meaningful(spec) { spec } else { name };
        if !item.is_empty() {
            items.push(item);
        }
    }
    let item_name = items.join(", ").chars().take(10).collect();
    let amount = get("AmountInFiguers");
    let amount = if amount.is_empty() {
        get("TotalAmount")
    } else {
        amount
    };
    Ok(OcrFields {
        purchase_date: normalize_date(&get("InvoiceDate")).unwrap_or_default(),
        amount_cents: amount_to_cents(&amount).unwrap_or(0),
        item_name,
        invoice_number: get("InvoiceNum"),
        seller_name: get("SellerName"),
        seller_tax_id: get("SellerRegisterNum"),
    })
}

pub async fn recognize(
    bytes: &[u8],
    extension: &str,
    api_key: &str,
    secret_key: &str,
    consent: bool,
) -> Result<OcrFields> {
    ensure!(consent, "尚未明确同意云端 OCR；发票没有上传");
    ensure!(
        !api_key.trim().is_empty() && !secret_key.trim().is_empty(),
        "请先配置系统安全存储中的百度 OCR 凭据"
    );
    validate_file(&format!("invoice.{extension}"), bytes)?;
    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(45))
        .redirect(reqwest::redirect::Policy::none())
        .build()?;
    // Credentials use a POST body, never URLs, logs, export manifests or frontend responses.
    let token = client
        .post("https://aip.baidubce.com/oauth/2.0/token")
        .form(&[
            ("grant_type", "client_credentials"),
            ("client_id", api_key),
            ("client_secret", secret_key),
        ])
        .send()
        .await
        .map_err(|_| anyhow::anyhow!("百度连接失败，请检查网络"))?
        .error_for_status()
        .map_err(|_| anyhow::anyhow!("百度授权请求失败"))?
        .json::<Value>()
        .await?;
    let token = token
        .get("access_token")
        .and_then(|v| v.as_str())
        .context("百度授权未成功，请检查 API Key 和 Secret Key")?;
    let field = if extension == "pdf" {
        "pdf_file"
    } else {
        "image"
    };
    let encoded = base64::engine::general_purpose::STANDARD.encode(bytes);
    let mut form = vec![(field, encoded)];
    if extension == "pdf" {
        form.push(("pdf_file_num", "1".into()));
    }
    let response = client
        .post("https://aip.baidubce.com/rest/2.0/ocr/v1/vat_invoice")
        .query(&[("access_token", token)])
        .form(&form)
        .send()
        .await
        .map_err(|_| anyhow::anyhow!("OCR 连接失败，可保留草稿并重试"))?
        .error_for_status()
        .map_err(|_| anyhow::anyhow!("OCR 请求失败，可检查额度后重试"))?
        .json::<Value>()
        .await?;
    parse_response(&response)
}
