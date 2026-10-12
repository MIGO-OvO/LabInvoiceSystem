use crate::{model::*, security::*, Repository};
use anyhow::{ensure, Context, Result};
use calamine::{Data, DataType, Reader, Xlsx};
use std::{
    collections::{HashMap, HashSet},
    fs::{self, File},
    io::{Cursor, Write},
    path::{Path, PathBuf},
    sync::atomic::AtomicBool,
};

const HEADERS: [&str; 8] = [
    "发票录入日期",
    "购买日期",
    "金额",
    "项目名称",
    "支付方式",
    "发票号码",
    "销售方名称",
    "销售方税号",
];

fn date_value(value: &Data) -> String {
    if let Some(date) = value.as_datetime() {
        return date.date().to_string();
    }
    normalize_date(&value.to_string()).unwrap_or_default()
}
pub fn normalize_date(value: &str) -> Option<String> {
    let value = value.trim();
    let first = value
        .split('T')
        .next()
        .unwrap_or(value)
        .split(' ')
        .next()
        .unwrap_or(value);
    for format in ["%Y-%m-%d", "%Y%m%d", "%Y/%m/%d", "%Y年%m月%d日"] {
        if let Ok(date) = chrono::NaiveDate::parse_from_str(first, format) {
            return Some(date.to_string());
        }
    }
    None
}

fn spreadsheet(bytes: &[u8], container: &str, container_hash: &str) -> Result<Vec<SpreadsheetRow>> {
    // XLSX is itself a ZIP. Check its expansion before handing it to the parser.
    let (mut inner, names) =
        crate::zip_index::indexed_zip(Cursor::new(bytes), &AtomicBool::new(false))?;
    ensure!(
        validate_zip_names(&mut inner, Some(&names))? <= 64 * 1024 * 1024,
        "Excel 解压后超过64 MiB"
    );
    let mut book: Xlsx<_> = Xlsx::new(Cursor::new(bytes))?;
    let name = book
        .sheet_names()
        .first()
        .context("Excel 没有工作表")?
        .clone();
    let sheet = book.worksheet_range(&name)?;
    ensure!(
        sheet.height() <= MAX_ENTRIES + 1 && sheet.width() <= 64,
        "Excel 行列数量超出限额"
    );
    let mut rows = sheet.rows();
    let header = rows.next().context("Excel 没有表头")?;
    let columns: HashMap<_, _> = header
        .iter()
        .enumerate()
        .map(|(i, v)| (v.to_string().trim().to_owned(), i))
        .collect();
    ensure!(
        HEADERS.iter().all(|h| columns.contains_key(*h)),
        "Excel 缺少旧版八字段表头，不能自动匹配"
    );
    let mut result = Vec::new();
    for (index, row) in rows.enumerate() {
        if row.iter().all(|v| v.to_string().trim().is_empty()) {
            continue;
        }
        let get = |label: &str| -> Data {
            columns
                .get(label)
                .and_then(|i| row.get(*i))
                .cloned()
                .unwrap_or(Data::Empty)
        };
        // Numeric XLSX cells are checked for representable cents; strings preserve leading-zero identities.
        let amount = get("金额");
        let cents = match &amount {
            Data::Float(v) => {
                ensure!(
                    v.is_finite()
                        && *v >= 0.0
                        && ((*v * 100.0).round() - *v * 100.0).abs() < 0.00001,
                    "Excel 第{}行金额精度无效",
                    index + 2
                );
                amount_to_cents(&format!("{v:.2}"))?
            }
            _ => amount_to_cents(&amount.to_string())?,
        };
        let category = match get("分类").to_string().trim() {
            "办公/生活用品" | "officeLiving" => Category::OfficeLiving,
            "实验用品" | "labSupplies" => Category::LabSupplies,
            _ => Category::Uncategorized,
        };
        let item = SpreadsheetRow {
            id: new_id(),
            container: container.into(),
            container_hash: container_hash.into(),
            row_number: index + 2,
            entry_date: date_value(&get("发票录入日期")),
            purchase_date: date_value(&get("购买日期")),
            amount_cents: cents,
            item_name: get("项目名称").to_string(),
            payment_method: get("支付方式").to_string(),
            invoice_number: get("发票号码").to_string(),
            seller_name: get("销售方名称").to_string(),
            seller_tax_id: get("销售方税号").to_string(),
            category,
            mapped_file: get("原始文件").to_string(),
        };
        ensure!(
            !item.entry_date.is_empty() && !item.purchase_date.is_empty(),
            "Excel 第{}行日期无效",
            index + 2
        );
        result.push(item);
    }
    Ok(result)
}

pub fn apply_filename(invoice: &mut Invoice) {
    let stem = Path::new(&invoice.file_name)
        .file_stem()
        .map(|v| v.to_string_lossy().to_string())
        .unwrap_or_default();
    let parts: Vec<_> = stem.split('-').collect();
    if parts.len() < 4 {
        return;
    }
    if let Some(date) = normalize_date(parts[0]) {
        invoice.purchase_date = date;
    }
    if let Ok(amount) = amount_to_cents(parts[parts.len() - 1].trim_end_matches("元")) {
        invoice.amount_cents = amount;
    }
    invoice.payment_method = parts[parts.len() - 2].into();
    invoice.item_name = parts[1..parts.len() - 2].join("-");
    invoice
        .warnings
        .push("文件名解析仅供辅助，请与原件核对".into());
}

fn legacy_metadata(bytes: &[u8], invoice: &mut Invoice) -> Result<()> {
    ensure!(bytes.len() <= 1024 * 1024, "旧 JSON 过大");
    let data: serde_json::Value = serde_json::from_slice(bytes)?;
    let text = |key: &str| {
        data.get(key)
            .and_then(|v| v.as_str())
            .unwrap_or("")
            .to_owned()
    };
    invoice.purchase_date = normalize_date(&text("InvoiceDate")).context("旧元数据购买日期无效")?;
    let entry = text("EntryDate");
    if let Some(date) = normalize_date(&entry) {
        invoice.entry_date = date;
    } else {
        invoice.entry_date = String::new();
        invoice
            .warnings
            .push("旧元数据无录入日期，请人工填写；未使用当前日期替代历史日期".into());
    }
    let amount = data.get("Amount").context("旧元数据无金额")?;
    invoice.amount_cents = amount_to_cents(
        &amount
            .as_str()
            .map(str::to_owned)
            .unwrap_or_else(|| amount.to_string()),
    )?;
    invoice.item_name = text("ItemName");
    invoice.payment_method = text("PaymentMethod");
    invoice.invoice_number = text("InvoiceNumber");
    invoice.seller_name = text("SellerName");
    invoice.seller_tax_id = text("SellerTaxId");
    invoice.category = Category::Uncategorized;
    Ok(())
}

struct Package {
    invoices: Vec<Invoice>,
    rows: Vec<SpreadsheetRow>,
    issues: Vec<Issue>,
}
fn progress(job: &mut ImportJob, count: u64, notify: &mut impl FnMut(&ImportJob)) {
    job.bytes_read += count;
    notify(job);
}
fn parse_zip(
    repo: &Repository,
    path: &Path,
    job: &mut ImportJob,
    cancel: &AtomicBool,
    notify: &mut impl FnMut(&ImportJob),
) -> Result<Package> {
    let file = File::open(path)?;
    ensure!(
        file.metadata()?.len() <= MAX_TOTAL_BYTES,
        "ZIP 文件超过512 MiB"
    );
    let container = path
        .file_name()
        .context("文件名无效")?
        .to_string_lossy()
        .to_string();
    let container_hash = digest_reader(File::open(path)?, cancel)?;
    let (mut zip, original_names) =
        crate::zip_index::indexed_zip(file, cancel).context("ZIP 已损坏或格式无效")?;
    validate_zip_names(&mut zip, Some(&original_names))?;
    for (i, name) in original_names.iter().enumerate() {
        let entry = zip.by_index(i)?;
        if !entry.is_dir()
            && (is_invoice(name) || extension(name) == "xlsx" || extension(name) == "json")
        {
            job.total_bytes += entry.size();
        }
    }
    let mut file_names = Vec::new();
    for (i, name) in original_names.iter().enumerate() {
        let entry = zip.by_index(i)?;
        if !entry.is_dir() && is_invoice(name) {
            file_names.push((i, name.clone()));
        }
    }
    ensure!(!file_names.is_empty(), "ZIP 未包含受支持的发票原件");
    job.total_files += file_names.len();
    notify(job);
    let mut manifest = None;
    let mut rows = Vec::new();
    let mut issues = Vec::new();
    if let Ok(mut entry) = zip.by_name("manifest.json") {
        let bytes = read_limited(&mut entry, 8 * 1024 * 1024, cancel, |n| {
            progress(job, n, notify)
        })?;
        let parsed: Manifest =
            serde_json::from_slice(&bytes).context("Manifest 已损坏，禁止回退猜测")?;
        parsed.validate()?;
        manifest = Some(parsed);
    }
    for i in 0..zip.len() {
        let mut entry = zip.by_index(i)?;
        if extension(entry.name()) == "xlsx" {
            let member = entry.name().to_owned();
            let bytes = read_limited(&mut entry, MAX_FILE_BYTES, cancel, |n| {
                progress(job, n, notify)
            })?;
            match spreadsheet(&bytes, &container, &container_hash) {
                Ok(parsed) => rows.extend(parsed),
                Err(error) => issues.push(Issue {
                    container: container.clone(),
                    member,
                    message: format!("{error:#}"),
                }),
            };
        }
    }
    let mut mappings = HashMap::new();
    if let Some(manifest) = &manifest {
        let invoice_names: HashSet<_> = file_names.iter().map(|(_, name)| name.clone()).collect();
        for entry in &manifest.invoices {
            safe_member(&entry.path)?;
            ensure!(
                invoice_names.contains(&entry.path),
                "Manifest 引用缺失原件 {}",
                entry.path
            );
            ensure!(
                mappings.insert(entry.path.clone(), entry).is_none(),
                "Manifest 重复原件映射"
            );
            entry.invoice.validate()?;
            ensure!(
                entry.size == entry.invoice.file_size && entry.sha256 == entry.invoice.blob_hash,
                "Manifest 原件标识不一致"
            );
            ensure!(
                entry.invoice.sources.len() <= 100
                    && serde_json::to_vec(&entry.invoice)?.len() <= 256 * 1024,
                "Manifest 来源或元数据超限"
            );
        }
        ensure!(
            mappings.len() == file_names.len(),
            "Manifest 未精确覆盖全部原件，需修复后导入"
        );
    }
    let mut invoices = Vec::new();
    let mut occurrences = HashMap::new();
    for (_, name) in &file_names {
        *occurrences.entry(name.clone()).or_insert(0usize) += 1;
    }
    for (member_index, member) in file_names {
        cancelled(cancel)?;
        let mut entry = zip.by_index(member_index)?;
        let bytes = read_limited(&mut entry, MAX_FILE_BYTES, cancel, |n| {
            progress(job, n, notify)
        })?;
        drop(entry);
        let (hash, ext) = repo.put_blob(&member, &bytes)?;
        let name = Path::new(&member)
            .file_name()
            .context("原件文件名无效")?
            .to_string_lossy();
        let mut invoice = Invoice::draft(&job.id, &name, &hash, &ext, bytes.len() as u64);
        let mut kind = "filename";
        if let Some(record) = mappings.get(&member) {
            ensure!(
                record.sha256 == hash && record.size == bytes.len() as u64,
                "Manifest 校验不符: {member}"
            );
            ensure!(record.invoice.extension == ext, "Manifest 扩展名不符");
            let new_id = invoice.id.clone();
            invoice = record.invoice.clone();
            invoice.id = new_id;
            invoice.batch_id = job.id.clone();
            invoice.state = "draft".into();
            invoice.reviewed = false;
            invoice.previous_state = None;
            invoice.duplicate_of = None;
            kind = "manifest-v1";
            invoice.warnings =
                vec!["Manifest 完整性校验通过，来源仍需核对；校验和不是签名或真实性证明".into()];
        } else {
            apply_filename(&mut invoice);
            let metadata_name = Path::new(&member)
                .with_extension("json")
                .to_string_lossy()
                .to_string();
            if occurrences[&member] > 1 {
                invoice.warnings.push(format!(
                    "旧 ZIP 含同名原件，按条目 {} 保留；JSON/Excel 对应须人工确认",
                    member_index
                ));
            } else if let Ok(mut metadata) = zip.by_name(&metadata_name) {
                let bytes = read_limited(&mut metadata, 1024 * 1024, cancel, |n| {
                    progress(job, n, notify)
                })?;
                match legacy_metadata(&bytes, &mut invoice) {
                    Ok(()) => kind = "legacy-json",
                    Err(error) => issues.push(Issue {
                        container: container.clone(),
                        member: metadata_name,
                        message: format!("元数据无法使用: {error:#}"),
                    }),
                };
            }
        }
        invoice.sources.push(Source {
            batch_id: job.id.clone(),
            container_name: container.clone(),
            container_hash: container_hash.clone(),
            member_name: member.clone(),
            member_index: Some(member_index),
            metadata_kind: kind.into(),
        });
        invoices.push(invoice);
        job.processed_files += 1;
        notify(job);
    }
    if manifest.is_some() {
        rows.clear();
    } else {
        // Match by an explicit file mapping or a unique complete legacy filename tuple.
        // Rows with the same tuple or files with the same tuple are never guessed.
        let mut used = HashSet::new();
        let mut assignments = Vec::new();
        for (index, invoice) in invoices.iter().enumerate() {
            if invoice.sources.last().map(|s| s.metadata_kind.as_str()) == Some("legacy-json") {
                continue;
            }
            let member = &invoice.sources.last().context("来源缺失")?.member_name;
            let matches: Vec<_> = rows
                .iter()
                .enumerate()
                .filter(|(_, row)| {
                    if !row.mapped_file.is_empty() {
                        return &row.mapped_file == member;
                    }
                    row.purchase_date == invoice.purchase_date
                        && row.amount_cents == invoice.amount_cents
                        && row.item_name.replace('-', "_") == invoice.item_name
                        && row.payment_method.replace('-', "_") == invoice.payment_method
                })
                .map(|(i, _)| i)
                .collect();
            if matches.len() == 1 {
                let row_index = matches[0];
                let occurrences = invoices
                    .iter()
                    .filter(|other| {
                        if !rows[row_index].mapped_file.is_empty() {
                            other
                                .sources
                                .last()
                                .is_some_and(|s| s.member_name == rows[row_index].mapped_file)
                        } else {
                            other.purchase_date == invoice.purchase_date
                                && other.amount_cents == invoice.amount_cents
                                && other.item_name == invoice.item_name
                                && other.payment_method == invoice.payment_method
                        }
                    })
                    .count();
                if occurrences == 1 {
                    assignments.push((index, row_index));
                }
            }
        }
        for (index, row_index) in assignments {
            if used.insert(row_index) {
                rows[row_index].apply(&mut invoices[index]);
                invoices[index].warnings.push(format!(
                    "Excel 第{}行唯一匹配，仍须人工核对",
                    rows[row_index].row_number
                ));
                if let Some(source) = invoices[index].sources.last_mut() {
                    source.metadata_kind = "legacy-excel".into();
                }
            }
        }
        rows = rows
            .into_iter()
            .enumerate()
            .filter_map(|(i, r)| if used.contains(&i) { None } else { Some(r) })
            .collect();
        for invoice in &mut invoices {
            if !rows.is_empty() {
                invoice
                    .warnings
                    .push("此压缩包还有未匹配 Excel 行，请在导入预览中指定原件".into());
            }
        }
    }
    Ok(Package {
        invoices,
        rows,
        issues,
    })
}

fn raw_file(
    repo: &Repository,
    path: &Path,
    job: &mut ImportJob,
    cancel: &AtomicBool,
    notify: &mut impl FnMut(&ImportJob),
) -> Result<Package> {
    let name = path
        .file_name()
        .context("文件名无效")?
        .to_string_lossy()
        .to_string();
    ensure!(is_invoice(&name), "只支持 PDF/JPG/PNG/ZIP 或旧归档文件夹");
    let size = fs::metadata(path)?.len();
    ensure!(size <= MAX_FILE_BYTES, "单文件超过32 MiB");
    job.total_files += 1;
    job.total_bytes += size;
    notify(job);
    let bytes = read_limited(File::open(path)?, MAX_FILE_BYTES, cancel, |n| {
        progress(job, n, notify)
    })?;
    let (hash, ext) = repo.put_blob(&name, &bytes)?;
    let mut invoice = Invoice::draft(&job.id, &name, &hash, &ext, size);
    apply_filename(&mut invoice);
    let mut kind = "manual";
    let mut issues = Vec::new();
    let metadata = path.with_extension("json");
    if metadata.is_file() {
        let bytes = read_limited(File::open(&metadata)?, 1024 * 1024, cancel, |_| {})?;
        match legacy_metadata(&bytes, &mut invoice) {
            Ok(()) => kind = "legacy-json",
            Err(error) => issues.push(Issue {
                container: name.clone(),
                member: metadata
                    .file_name()
                    .unwrap_or_default()
                    .to_string_lossy()
                    .into(),
                message: format!("元数据无法使用: {error:#}"),
            }),
        };
    }
    invoice.sources.push(Source {
        batch_id: job.id.clone(),
        container_name: name.clone(),
        container_hash: hash,
        member_name: name,
        member_index: None,
        metadata_kind: kind.into(),
    });
    job.processed_files += 1;
    notify(job);
    Ok(Package {
        invoices: vec![invoice],
        rows: vec![],
        issues,
    })
}

fn commit_package(repo: &mut Repository, package: Package, job: &mut ImportJob) -> Result<()> {
    let mut records = repo.list()?;
    let mut added = Vec::new();
    let mut duplicates = 0;
    let mut issues = package.issues;
    for mut invoice in package.invoices {
        let found = records.iter().position(|other| {
            other.state != "deleted"
                && (other.blob_hash == invoice.blob_hash || identity_matches(other, &invoice))
        });
        if let Some(index) = found {
            let other = &mut records[index];
            let same_metadata = invoice.entry_date == other.entry_date
                && invoice.purchase_date == other.purchase_date
                && invoice.amount_cents == other.amount_cents
                && invoice.category == other.category
                && invoice.item_name == other.item_name
                && invoice.payment_method == other.payment_method
                && invoice.invoice_number == other.invoice_number
                && invoice.seller_name == other.seller_name
                && invoice.seller_tax_id == other.seller_tax_id;
            let empty_manual = invoice.amount_cents == 0
                && invoice.item_name.is_empty()
                && invoice.invoice_number.is_empty();
            if other.blob_hash == invoice.blob_hash && (same_metadata || empty_manual) {
                for source in invoice.sources {
                    if !other.sources.contains(&source) {
                        other.sources.push(source);
                    }
                }
                duplicates += 1;
                issues.push(Issue {
                    container: other
                        .sources
                        .last()
                        .map(|s| s.container_name.clone())
                        .unwrap_or_default(),
                    member: invoice.file_name,
                    message: format!("完全重复原件，保留已有记录 {} 并追加来源", other.id),
                });
                continue;
            }
            invoice.duplicate_of = Some(other.id.clone());
            invoice.warnings.push(format!(
                "与 {} 存在{}冲突：请保留已有、合并核对信息或核实不同发票",
                other.id,
                if other.blob_hash == invoice.blob_hash {
                    "相同文件元数据"
                } else {
                    "发票身份"
                }
            ));
            duplicates += 1;
        }
        added.push(invoice.id.clone());
        records.push(invoice);
    }
    repo.db.execute_batch("BEGIN IMMEDIATE")?;
    let mut next = job.clone();
    next.success += added.len();
    next.duplicates += duplicates;
    next.issues.extend(issues);
    next.unassigned_rows.extend(package.rows);
    next.invoice_ids.extend(added);
    let result = (|| {
        for invoice in &records {
            repo.put(invoice)?;
        }
        repo.save_job(&next)?;
        repo.audit("import-package", &job.id)?;
        Ok(())
    })();
    repo.finish(result)?;
    *job = next;
    Ok(())
}

pub fn import_paths(
    repo: &mut Repository,
    paths: &[PathBuf],
    mut job: ImportJob,
    cancel: &AtomicBool,
    mut notify: impl FnMut(&ImportJob),
) -> Result<ImportJob> {
    ensure!(
        !paths.is_empty() && paths.len() <= 128,
        "一次可导入1至128个来源"
    );
    job.state = "running".into();
    job.total_containers = paths.len();
    job.input_paths = paths
        .iter()
        .map(|p| p.to_string_lossy().to_string())
        .collect();
    repo.save_job(&job)?;
    repo.snapshot("import")?;
    notify(&job);
    for path in paths {
        if cancelled(cancel).is_err() {
            break;
        }
        let result = if path.is_dir() {
            import_legacy_folder(repo, path, &mut job, cancel, &mut notify)
        } else if extension(&path.to_string_lossy()) == "zip" {
            parse_zip(repo, path, &mut job, cancel, &mut notify).and_then(|package| {
                cancelled(cancel)?;
                commit_package(repo, package, &mut job)
            })
        } else {
            raw_file(repo, path, &mut job, cancel, &mut notify).and_then(|package| {
                cancelled(cancel)?;
                commit_package(repo, package, &mut job)
            })
        };
        if let Err(error) = result {
            job.failed += 1;
            job.issues.push(Issue {
                container: path
                    .file_name()
                    .unwrap_or_default()
                    .to_string_lossy()
                    .into(),
                member: String::new(),
                message: format!("{error:#}"),
            });
        }
        job.processed_containers += 1;
        repo.save_job(&job)?;
        notify(&job);
    }
    job.state = if cancel.load(std::sync::atomic::Ordering::Acquire) {
        "cancelled"
    } else if job.failed > 0 {
        "completedWithErrors"
    } else {
        "completed"
    }
    .into();
    repo.save_job(&job)?;
    notify(&job);
    Ok(job)
}

pub fn scan_legacy(path: &Path) -> Result<Vec<PathBuf>> {
    let root = path.canonicalize()?;
    let mut files = Vec::new();
    let mut pending = vec![root.clone()];
    let mut scanned = 0;
    while let Some(directory) = pending.pop() {
        for entry in fs::read_dir(directory)? {
            let entry = entry?;
            scanned += 1;
            ensure!(
                scanned <= 20_000,
                "旧目录文件数量超限，请选择 archive_data 文件夹"
            );
            let ty = entry.file_type()?;
            ensure!(!ty.is_symlink(), "旧归档包含符号链接，已停止迁移");
            let path = entry.path();
            ensure!(path.canonicalize()?.starts_with(&root), "旧归档路径越界");
            if ty.is_dir() {
                pending.push(path);
            } else if is_invoice(&path.to_string_lossy()) {
                files.push(path);
                ensure!(
                    files.len() <= MAX_ENTRIES,
                    "单批迁移最多2048张发票，请分月导入"
                );
            }
        }
    }
    files.sort();
    ensure!(!files.is_empty(), "目录没有可迁移发票");
    Ok(files)
}

fn import_legacy_folder(
    repo: &mut Repository,
    path: &Path,
    job: &mut ImportJob,
    cancel: &AtomicBool,
    notify: &mut impl FnMut(&ImportJob),
) -> Result<()> {
    let files = scan_legacy(path)?;
    let root = path.canonicalize()?;
    let backup = repo
        .root
        .join("backups")
        .join(format!("legacy-originals-{}.zip", new_id()));
    let writer = File::options().write(true).create_new(true).open(&backup)?;
    let mut zip = zip::ZipWriter::new(writer);
    let options =
        zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Stored);
    let mut total = 0u64;
    for file in &files {
        for source in [file.clone(), file.with_extension("json")] {
            if source.is_file() {
                cancelled(cancel)?;
                let bytes = read_limited(File::open(&source)?, MAX_FILE_BYTES, cancel, |_| {})?;
                total += bytes.len() as u64;
                ensure!(total <= MAX_TOTAL_BYTES, "迁移备份超过512 MiB，请分月导入");
                let name = source
                    .strip_prefix(&root)?
                    .to_string_lossy()
                    .replace('\\', "/");
                safe_member(&name)?;
                zip.start_file(name, options)?;
                zip.write_all(&bytes)?;
            }
        }
    }
    let file = zip.finish()?;
    file.sync_all()?;
    repo.audit(
        "legacy-backup",
        backup
            .file_name()
            .unwrap_or_default()
            .to_string_lossy()
            .as_ref(),
    )?;
    let mut package = Package {
        invoices: vec![],
        rows: vec![],
        issues: vec![],
    };
    for path in files {
        let mut next = raw_file(repo, &path, job, cancel, notify)?;
        for invoice in &mut next.invoices {
            if let Some(source) = invoice.sources.last_mut() {
                source.container_name = root
                    .file_name()
                    .unwrap_or_default()
                    .to_string_lossy()
                    .into();
                source.member_name = path
                    .strip_prefix(&root)?
                    .to_string_lossy()
                    .replace('\\', "/");
                source.metadata_kind = "legacy-migration".into();
            }
            invoice
                .warnings
                .push("旧原件和 JSON 已备份；这是复制导入，旧目录保持原样".into());
        }
        package.invoices.extend(next.invoices);
        package.issues.extend(next.issues);
    }
    cancelled(cancel)?;
    commit_package(repo, package, job)
}

pub fn import_legacy_settings(repo: &Repository, path: &Path) -> Result<Settings> {
    ensure!(fs::metadata(path)?.len() <= 1024 * 1024, "旧设置过大");
    let bytes = fs::read(path)?;
    let data: serde_json::Value = serde_json::from_slice(&bytes)?;
    let backup = repo
        .root
        .join("backups")
        .join(format!("legacy-settings-{}.json", new_id()));
    // Backup excludes credentials; the original config remains the rollback source.
    let mut safe = data.clone();
    if let Some(object) = safe.as_object_mut() {
        for key in [
            "BaiduApiKey",
            "BaiduSecretKey",
            "BaiduSecretKeyEncrypted",
            "BaiduAppId",
        ] {
            object.remove(key);
        }
    }
    crate::store::write_new(&backup, &serde_json::to_vec_pretty(&safe)?)?;
    let mut settings = repo.settings()?;
    settings.theme = if data.get("ThemeMode").and_then(|v| v.as_str()) == Some("Light") {
        "light"
    } else {
        "dark"
    }
    .into();
    settings.export_directory = data
        .get("ExportDirectory")
        .and_then(|v| v.as_str())
        .filter(|v| Path::new(v).is_absolute())
        .unwrap_or("")
        .into();
    settings.cloud_ocr_consent = false;
    settings.monthly_usage = data
        .get("BaiduMonthlyUsage")
        .and_then(|v| v.as_u64())
        .unwrap_or(0)
        .min(u32::MAX as u64) as u32;
    settings.monthly_quota = data
        .get("BaiduMonthlyQuota")
        .and_then(|v| v.as_u64())
        .unwrap_or(1000)
        .min(1_000_000) as u32;
    settings.usage_month = data
        .get("BaiduUsageMonth")
        .and_then(|v| v.as_str())
        .unwrap_or("")
        .into();
    if let Some(corrections) = data.get("ItemNameCorrections") {
        settings.item_corrections = serde_json::from_value(corrections.clone())?;
    }
    repo.save_settings(&settings)?;
    repo.audit(
        "legacy-settings",
        "导入非敏感配置；OCR 同意重新确认，密钥需安全存储",
    )?;
    Ok(settings)
}
