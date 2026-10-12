use anyhow::{bail, ensure, Result};
use chrono::{Local, NaiveDate};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

pub fn today() -> String {
    Local::now().date_naive().to_string()
}
pub fn new_id() -> String {
    uuid::Uuid::new_v4().to_string()
}

#[derive(Debug, Clone, Copy, Default, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
#[serde(rename_all = "camelCase")]
pub enum Category {
    OfficeLiving,
    LabSupplies,
    #[default]
    Uncategorized,
}
impl Category {
    pub fn label(self) -> &'static str {
        match self {
            Self::OfficeLiving => "办公/生活用品",
            Self::LabSupplies => "实验用品",
            Self::Uncategorized => "未分类（历史数据）",
        }
    }
    pub fn folder(self) -> &'static str {
        match self {
            Self::OfficeLiving => "office-living",
            Self::LabSupplies => "lab-supplies",
            Self::Uncategorized => "uncategorized",
        }
    }
}

#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Source {
    pub batch_id: String,
    pub container_name: String,
    pub container_hash: String,
    pub member_name: String,
    #[serde(default)]
    pub member_index: Option<usize>,
    pub metadata_kind: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Invoice {
    pub id: String,
    pub batch_id: String,
    pub file_name: String,
    pub blob_hash: String,
    pub extension: String,
    pub file_size: u64,
    pub category: Category,
    pub entry_date: String,
    pub purchase_date: String,
    pub amount_cents: i64,
    pub item_name: String,
    pub payment_method: String,
    pub invoice_number: String,
    pub seller_name: String,
    pub seller_tax_id: String,
    pub state: String,
    pub sources: Vec<Source>,
    #[serde(default)]
    pub warnings: Vec<String>,
    #[serde(default)]
    pub ocr_item_name: String,
    #[serde(default)]
    pub duplicate_of: Option<String>,
    #[serde(default)]
    pub previous_state: Option<String>,
    #[serde(default)]
    pub reviewed: bool,
    #[serde(default)]
    pub duplicate_reason: String,
}
impl Invoice {
    pub fn draft(batch: &str, name: &str, hash: &str, extension: &str, size: u64) -> Self {
        Self {
            id: new_id(),
            batch_id: batch.into(),
            file_name: name.into(),
            blob_hash: hash.into(),
            extension: extension.into(),
            file_size: size,
            category: Category::Uncategorized,
            entry_date: today(),
            purchase_date: String::new(),
            amount_cents: 0,
            item_name: String::new(),
            payment_method: "公务卡".into(),
            invoice_number: String::new(),
            seller_name: String::new(),
            seller_tax_id: String::new(),
            state: "draft".into(),
            sources: vec![],
            warnings: vec![],
            ocr_item_name: String::new(),
            duplicate_of: None,
            previous_state: None,
            reviewed: false,
            duplicate_reason: String::new(),
        }
    }
    pub fn validate(&self) -> Result<()> {
        ensure!(!self.item_name.trim().is_empty(), "请填写项目名称");
        ensure!(
            self.amount_cents > 0 && self.amount_cents <= 999_999_999_999,
            "金额须大于0且不超过99.99亿元"
        );
        for (label, value) in [
            ("录入日期", &self.entry_date),
            ("购买日期", &self.purchase_date),
        ] {
            ensure!(
                NaiveDate::parse_from_str(value, "%Y-%m-%d").is_ok() && value.len() == 10,
                "{label}无效"
            );
        }
        ensure!(!self.payment_method.trim().is_empty(), "请填写支付方式");
        for value in [
            &self.item_name,
            &self.payment_method,
            &self.invoice_number,
            &self.seller_name,
            &self.seller_tax_id,
        ] {
            ensure!(
                value.len() <= 2048 && !value.chars().any(|c| c == '\0'),
                "业务字段过长或包含非法字符"
            );
        }
        Ok(())
    }
}

pub fn amount_to_cents(value: &str) -> Result<i64> {
    let value = value
        .trim()
        .trim_start_matches(['¥', '￥'])
        .replace(',', "");
    ensure!(!value.is_empty() && !value.starts_with('-'), "金额无效");
    let parts: Vec<_> = value.split('.').collect();
    ensure!(
        parts.len() <= 2 && parts[0].chars().all(|c| c.is_ascii_digit()),
        "金额无效"
    );
    let whole: i64 = parts[0].parse()?;
    let fraction = parts.get(1).copied().unwrap_or("");
    ensure!(
        fraction.len() <= 2 && fraction.chars().all(|c| c.is_ascii_digit()),
        "金额最多两位小数"
    );
    let cents = match fraction.len() {
        0 => 0,
        1 => fraction.parse::<i64>()? * 10,
        2 => fraction.parse::<i64>()?,
        _ => unreachable!(),
    };
    whole
        .checked_mul(100)
        .and_then(|v| v.checked_add(cents))
        .filter(|v| *v <= 999_999_999_999)
        .ok_or_else(|| anyhow::anyhow!("金额超出范围"))
}

pub fn identity_matches(left: &Invoice, right: &Invoice) -> bool {
    fn norm(v: &str) -> String {
        v.trim().to_uppercase()
    }
    if left.invoice_number.trim().is_empty()
        || norm(&left.invoice_number) != norm(&right.invoice_number)
    {
        return false;
    }
    if !left.seller_tax_id.trim().is_empty() && !right.seller_tax_id.trim().is_empty() {
        return norm(&left.seller_tax_id) == norm(&right.seller_tax_id);
    }
    if !left.seller_name.trim().is_empty() && !right.seller_name.trim().is_empty() {
        return norm(&left.seller_name) == norm(&right.seller_name);
    }
    true
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Issue {
    pub container: String,
    pub member: String,
    pub message: String,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ImportJob {
    pub id: String,
    pub state: String,
    pub total_containers: usize,
    pub processed_containers: usize,
    pub total_files: usize,
    pub processed_files: usize,
    pub bytes_read: u64,
    pub total_bytes: u64,
    pub success: usize,
    pub duplicates: usize,
    pub failed: usize,
    pub issues: Vec<Issue>,
    pub unassigned_rows: Vec<SpreadsheetRow>,
    #[serde(default)]
    pub invoice_ids: Vec<String>,
    #[serde(default, skip_serializing)]
    pub input_paths: Vec<String>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpreadsheetRow {
    pub id: String,
    pub container: String,
    #[serde(default)]
    pub container_hash: String,
    pub row_number: usize,
    pub entry_date: String,
    pub purchase_date: String,
    pub amount_cents: i64,
    pub item_name: String,
    pub payment_method: String,
    pub invoice_number: String,
    pub seller_name: String,
    pub seller_tax_id: String,
    pub category: Category,
    pub mapped_file: String,
}
impl SpreadsheetRow {
    pub fn apply(&self, invoice: &mut Invoice) {
        invoice.entry_date.clone_from(&self.entry_date);
        invoice.purchase_date.clone_from(&self.purchase_date);
        invoice.amount_cents = self.amount_cents;
        invoice.item_name.clone_from(&self.item_name);
        invoice.payment_method.clone_from(&self.payment_method);
        invoice.invoice_number.clone_from(&self.invoice_number);
        invoice.seller_name.clone_from(&self.seller_name);
        invoice.seller_tax_id.clone_from(&self.seller_tax_id);
        invoice.category = self.category;
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Settings {
    pub theme: String,
    pub export_directory: String,
    pub cloud_ocr_consent: bool,
    pub monthly_usage: u32,
    pub monthly_quota: u32,
    pub usage_month: String,
    pub skipped_version: String,
    pub remind_after: i64,
    pub item_corrections: BTreeMap<String, String>,
}
impl Default for Settings {
    fn default() -> Self {
        Self {
            theme: "dark".into(),
            export_directory: String::new(),
            cloud_ocr_consent: false,
            monthly_usage: 0,
            monthly_quota: 1000,
            usage_month: String::new(),
            skipped_version: String::new(),
            remind_after: 0,
            item_corrections: BTreeMap::new(),
        }
    }
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Totals {
    pub count: usize,
    pub amount_cents: i64,
}
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Statistics {
    pub total: Totals,
    pub last30_cents: i64,
    pub categories: BTreeMap<Category, Totals>,
    pub daily: BTreeMap<String, i64>,
    pub payments: BTreeMap<String, Totals>,
}
pub fn statistics(invoices: &[Invoice], date: NaiveDate) -> Statistics {
    let mut stats = Statistics::default();
    for category in [
        Category::OfficeLiving,
        Category::LabSupplies,
        Category::Uncategorized,
    ] {
        stats.categories.insert(category, Totals::default());
    }
    for invoice in invoices.iter().filter(|i| i.state == "archived") {
        stats.total.count += 1;
        stats.total.amount_cents += invoice.amount_cents;
        let category = stats.categories.entry(invoice.category).or_default();
        category.count += 1;
        category.amount_cents += invoice.amount_cents;
        let payment = stats
            .payments
            .entry(invoice.payment_method.clone())
            .or_default();
        payment.count += 1;
        payment.amount_cents += invoice.amount_cents;
        *stats
            .daily
            .entry(invoice.purchase_date.clone())
            .or_default() += invoice.amount_cents;
        if let Ok(purchase) = NaiveDate::parse_from_str(&invoice.purchase_date, "%Y-%m-%d") {
            let days = (date - purchase).num_days();
            if (0..30).contains(&days) {
                stats.last30_cents += invoice.amount_cents;
            }
        }
    }
    stats
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ManifestEntry {
    pub path: String,
    pub sha256: String,
    pub size: u64,
    pub invoice: Invoice,
}
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Manifest {
    pub format: String,
    pub schema_version: u32,
    pub app_version: String,
    pub created_at: String,
    pub invoices: Vec<ManifestEntry>,
}
impl Manifest {
    pub fn validate(&self) -> Result<()> {
        if self.format != "LabInvoiceSystem" || self.schema_version != 1 {
            bail!("Manifest 格式或版本不受支持，请勿降级猜测导入");
        }
        ensure!(self.invoices.len() <= 2048, "Manifest 发票数量超限");
        Ok(())
    }
}
