use crate::{model::*, security::*, Repository};
use anyhow::{ensure, Context, Result};
use rust_xlsxwriter::Workbook;
use serde::{Deserialize, Serialize};
use std::{
    fs::{self, File},
    io::Write,
    path::{Path, PathBuf},
    sync::atomic::AtomicBool,
};

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExportProgress {
    pub completed: usize,
    pub total: usize,
    pub bytes_written: u64,
    pub total_bytes: u64,
}
fn safe_label(value: &str) -> String {
    let label: String = value
        .chars()
        .map(|c| {
            if c.is_control() || "/\\:*?\"<>|".contains(c) {
                '_'
            } else {
                c
            }
        })
        .take(60)
        .collect();
    let label = label.trim().trim_matches('.');
    if label.is_empty() {
        "invoice".into()
    } else {
        label.into()
    }
}

pub fn export_zip(
    repo: &Repository,
    ids: &[String],
    output: &Path,
    cancel: &AtomicBool,
    mut notify: impl FnMut(&ExportProgress),
) -> Result<Manifest> {
    ensure!(
        !ids.is_empty() && ids.len() <= MAX_ENTRIES,
        "请选择1至2048张归档发票"
    );
    ensure!(!output.exists(), "目标 ZIP 已存在，请选择新文件名");
    let mut unique = std::collections::HashSet::new();
    let mut invoices = Vec::new();
    for id in ids {
        ensure!(unique.insert(id), "导出选择重复");
        let invoice = repo.get(id)?;
        ensure!(invoice.state == "archived", "只能导出已归档发票");
        invoice.validate()?;
        invoices.push(invoice);
    }
    let mut entries = Vec::new();
    for invoice in &invoices {
        let path = format!(
            "invoices/{}/{}/{}/{}/{}-{}.{}",
            invoice.category.folder(),
            invoice.entry_date,
            invoice.purchase_date,
            safe_label(&invoice.payment_method),
            invoice.id,
            safe_label(&invoice.file_name),
            invoice.extension
        );
        safe_member(&path)?;
        entries.push(ManifestEntry {
            path,
            sha256: invoice.blob_hash.clone(),
            size: invoice.file_size,
            invoice: invoice.clone(),
        });
    }
    let manifest = Manifest {
        format: "LabInvoiceSystem".into(),
        schema_version: 1,
        app_version: env!("CARGO_PKG_VERSION").into(),
        created_at: chrono::Utc::now().to_rfc3339(),
        invoices: entries,
    };
    let mut book = Workbook::new();
    let sheet = book.add_worksheet();
    sheet.set_name("发票明细")?;
    let headers = [
        "发票录入日期",
        "购买日期",
        "金额",
        "项目名称",
        "支付方式",
        "发票号码",
        "销售方名称",
        "销售方税号",
        "分类",
        "原始文件",
    ];
    let header_format = rust_xlsxwriter::Format::new()
        .set_bold()
        .set_background_color("EEF2FF");
    for (col, header) in headers.iter().enumerate() {
        sheet.write_string_with_format(0, col as u16, *header, &header_format)?;
    }
    let money = rust_xlsxwriter::Format::new().set_num_format("0.00");
    for (index, record) in manifest.invoices.iter().enumerate() {
        let row = (index + 1) as u32;
        let invoice = &record.invoice;
        sheet.write_string(row, 0, &invoice.entry_date)?;
        sheet.write_string(row, 1, &invoice.purchase_date)?;
        sheet.write_number_with_format(row, 2, invoice.amount_cents as f64 / 100.0, &money)?;
        for (col, value) in [
            (3, &invoice.item_name),
            (4, &invoice.payment_method),
            (5, &invoice.invoice_number),
            (6, &invoice.seller_name),
            (7, &invoice.seller_tax_id),
        ] {
            sheet.write_string(row, col, value)?;
        }
        sheet.write_string(row, 8, invoice.category.label())?;
        sheet.write_string(row, 9, &record.path)?;
    }
    sheet.set_freeze_panes(1, 0)?;
    sheet.autofilter(0, 0, invoices.len() as u32, 9)?;
    sheet.set_column_range_width(0, 1, 16)?;
    sheet.set_column_width(2, 16)?;
    sheet.set_column_range_width(3, 9, 24)?;
    let excel = book.save_to_buffer()?;
    let manifest_bytes = serde_json::to_vec_pretty(&manifest)?;
    let parent = output.parent().context("导出目录无效")?;
    ensure!(parent.is_dir(), "导出目录不存在");
    let temporary = parent.join(format!(".labinvoice-export-{}.part", new_id()));
    let result = (|| -> Result<()> {
        let writer = File::options()
            .write(true)
            .create_new(true)
            .open(&temporary)?;
        let mut zip = zip::ZipWriter::new(writer);
        let options = zip::write::SimpleFileOptions::default()
            .compression_method(zip::CompressionMethod::Stored);
        let mut state = ExportProgress {
            total: invoices.len(),
            total_bytes: invoices.iter().map(|i| i.file_size).sum(),
            ..Default::default()
        };
        notify(&state);
        for record in &manifest.invoices {
            cancelled(cancel)?;
            let bytes = repo.read_invoice(&record.invoice.id)?;
            zip.start_file(&record.path, options)?;
            for chunk in bytes.chunks(65536) {
                cancelled(cancel)?;
                zip.write_all(chunk)?;
                state.bytes_written += chunk.len() as u64;
                notify(&state);
            }
            state.completed += 1;
            notify(&state);
        }
        zip.start_file("manifest.json", options)?;
        zip.write_all(&manifest_bytes)?;
        zip.start_file("发票明细.xlsx", options)?;
        zip.write_all(&excel)?;
        let file = zip.finish()?;
        file.sync_all()?;
        cancelled(cancel)?;
        fs::hard_link(&temporary, output).context("目标已存在或提交导出失败，原目标保持不变")?;
        Ok(())
    })();
    let _ = fs::remove_file(&temporary);
    result?;
    repo.audit("export", &ids.join(","))?;
    Ok(manifest)
}

pub fn default_export_name() -> String {
    format!(
        "LabInvoiceSystem-{}.zip",
        chrono::Local::now().format("%Y%m%d-%H%M%S")
    )
}
pub fn suggested_output(repo: &Repository) -> Result<PathBuf> {
    let settings = repo.settings()?;
    let directory = if settings.export_directory.is_empty() {
        repo.root.join("exports")
    } else {
        PathBuf::from(settings.export_directory)
    };
    fs::create_dir_all(&directory)?;
    Ok(directory.join(default_export_name()))
}
