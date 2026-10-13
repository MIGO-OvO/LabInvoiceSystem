use crate::{model::*, security::*};
use anyhow::{ensure, Context, Result};
use rusqlite::{params, Connection};
use std::{
    collections::HashSet,
    fs::{self, File, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
};

pub struct Repository {
    pub root: PathBuf,
    pub(crate) db: Connection,
}
impl Repository {
    pub fn open(root: impl AsRef<Path>) -> Result<Self> {
        let root = root.as_ref().to_owned();
        fs::create_dir_all(root.join("objects"))?;
        fs::create_dir_all(root.join("backups"))?;
        let db = Connection::open(root.join("invoices.sqlite3"))?;
        db.busy_timeout(std::time::Duration::from_secs(5))?;
        db.execute_batch("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS invoices(id TEXT PRIMARY KEY, hash TEXT NOT NULL, state TEXT NOT NULL, payload TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS invoices_hash ON invoices(hash);
            CREATE TABLE IF NOT EXISTS settings(id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS jobs(id TEXT PRIMARY KEY, payload TEXT NOT NULL, inputs TEXT NOT NULL DEFAULT '[]');
            CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY, at TEXT NOT NULL, action TEXT NOT NULL, detail TEXT NOT NULL);
            PRAGMA user_version=1;")?;
        let repo = Self { root, db };
        // A process crash never makes an unfinished import look successful.
        for mut job in repo.jobs()? {
            if matches!(job.state.as_str(), "running" | "queued" | "recognizing") {
                job.state = "interrupted".into();
                job.issues.push(Issue {
                    container: String::new(),
                    member: String::new(),
                    message: "上次操作未完成；已提交草稿保留，可重试其余来源".into(),
                });
                repo.save_job(&job)?;
            }
        }
        Ok(repo)
    }
    pub fn list(&self) -> Result<Vec<Invoice>> {
        let mut stmt = self
            .db
            .prepare("SELECT payload FROM invoices ORDER BY rowid DESC")?;
        let rows = stmt.query_map([], |r| r.get::<_, String>(0))?;
        rows.map(|row| Ok(serde_json::from_str(&row?)?)).collect()
    }
    pub fn get(&self, id: &str) -> Result<Invoice> {
        let json: String = self
            .db
            .query_row("SELECT payload FROM invoices WHERE id=?1", [id], |r| {
                r.get(0)
            })
            .context("发票不存在")?;
        Ok(serde_json::from_str(&json)?)
    }
    pub(crate) fn put(&self, invoice: &Invoice) -> Result<()> {
        self.db.execute("INSERT INTO invoices(id,hash,state,payload) VALUES(?1,?2,?3,?4) ON CONFLICT(id) DO UPDATE SET hash=excluded.hash,state=excluded.state,payload=excluded.payload", params![invoice.id, invoice.blob_hash, invoice.state, serde_json::to_string(invoice)?])?;
        Ok(())
    }
    pub fn put_blob(&self, name: &str, bytes: &[u8]) -> Result<(String, String)> {
        validate_file(name, bytes)?;
        let hash = digest(bytes);
        let ext = extension(name);
        let path = self.blob_path(&hash, &ext)?;
        if path.exists() {
            ensure!(
                digest(&fs::read(&path)?) == hash,
                "原件存储校验失败，请恢复备份"
            );
            return Ok((hash, ext));
        }
        let parent = path.parent().context("原件路径无效")?;
        fs::create_dir_all(parent)?;
        let temporary = parent.join(format!(".{}.part", new_id()));
        let result = (|| -> Result<()> {
            let mut file = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&temporary)?;
            file.write_all(bytes)?;
            file.sync_all()?;
            // hard_link creates the destination exclusively on both platforms; never replaces an original.
            match fs::hard_link(&temporary, &path) {
                Ok(()) => {}
                Err(error) if path.exists() => {
                    ensure!(
                        digest(&fs::read(&path)?) == hash,
                        "并发原件校验失败: {error}"
                    );
                }
                Err(error) => return Err(error.into()),
            };
            Ok(())
        })();
        let _ = fs::remove_file(&temporary);
        result?;
        Ok((hash, ext))
    }
    pub fn blob_path(&self, hash: &str, ext: &str) -> Result<PathBuf> {
        ensure!(
            hash.len() == 64
                && hash
                    .bytes()
                    .all(|c| c.is_ascii_digit() || (b'a'..=b'f').contains(&c))
                && matches!(ext, "pdf" | "png" | "jpg" | "jpeg"),
            "原件标识非法"
        );
        Ok(self
            .root
            .join("objects")
            .join(&hash[..2])
            .join(format!("{hash}.{ext}")))
    }
    pub fn read_invoice(&self, id: &str) -> Result<Vec<u8>> {
        let invoice = self.get(id)?;
        let path = self.blob_path(&invoice.blob_hash, &invoice.extension)?;
        ensure!(fs::metadata(&path)?.len() <= MAX_FILE_BYTES, "原件大小异常");
        let bytes = fs::read(path)?;
        ensure!(
            bytes.len() as u64 == invoice.file_size && digest(&bytes) == invoice.blob_hash,
            "原件 SHA-256 校验失败"
        );
        Ok(bytes)
    }
    pub fn snapshot(&self, purpose: &str) -> Result<String> {
        ensure!(
            purpose
                .chars()
                .all(|c| c.is_ascii_alphanumeric() || c == '-'),
            "备份标签非法"
        );
        let path = self.root.join("backups").join(format!(
            "{}-{purpose}-{}.sqlite3",
            chrono::Utc::now().format("%Y%m%dT%H%M%S"),
            new_id()
        ));
        let mut dest = Connection::open(&path)?;
        rusqlite::backup::Backup::new(&self.db, &mut dest)?.run_to_completion(
            64,
            std::time::Duration::from_millis(10),
            None,
        )?;
        dest.execute_batch("PRAGMA integrity_check;")?;
        Ok(path.to_string_lossy().into_owned())
    }
    pub fn settings(&self) -> Result<Settings> {
        let mut stmt = self.db.prepare("SELECT payload FROM settings WHERE id=1")?;
        let mut rows = stmt.query([])?;
        if let Some(row) = rows.next()? {
            Ok(serde_json::from_str(&row.get::<_, String>(0)?)?)
        } else {
            Ok(Settings::default())
        }
    }
    pub fn save_settings(&self, settings: &Settings) -> Result<()> {
        ensure!(
            matches!(settings.theme.as_str(), "dark" | "light" | "system"),
            "主题无效"
        );
        ensure!(
            settings.monthly_quota <= 1_000_000 && settings.item_corrections.len() <= 10_000,
            "设置超出范围"
        );
        self.db.execute("INSERT INTO settings(id,payload) VALUES(1,?1) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", [serde_json::to_string(settings)?])?;
        Ok(())
    }
    pub fn audit(&self, action: &str, detail: &str) -> Result<()> {
        self.db.execute(
            "INSERT INTO audit(at,action,detail) VALUES(?1,?2,?3)",
            params![chrono::Utc::now().to_rfc3339(), action, detail],
        )?;
        Ok(())
    }
    pub fn audit_log(&self) -> Result<Vec<(String, String, String)>> {
        let mut stmt = self
            .db
            .prepare("SELECT at,action,detail FROM audit ORDER BY id DESC LIMIT 200")?;
        let rows = stmt.query_map([], |r| Ok((r.get(0)?, r.get(1)?, r.get(2)?)))?;
        Ok(rows.collect::<rusqlite::Result<_>>()?)
    }
    pub fn jobs(&self) -> Result<Vec<ImportJob>> {
        let mut stmt = self
            .db
            .prepare("SELECT payload,inputs FROM jobs ORDER BY rowid DESC LIMIT 100")?;
        let rows = stmt.query_map([], |r| Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?)))?;
        rows.map(|row| {
            let (json, inputs) = row?;
            let mut job: ImportJob = serde_json::from_str(&json)?;
            job.input_paths = serde_json::from_str(&inputs)?;
            Ok(job)
        })
        .collect()
    }
    pub fn save_job(&self, job: &ImportJob) -> Result<()> {
        self.db.execute("INSERT INTO jobs(id,payload,inputs) VALUES(?1,?2,?3) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload,inputs=excluded.inputs",params![job.id,serde_json::to_string(job)?,serde_json::to_string(&job.input_paths)?])?;
        Ok(())
    }
    pub fn edit(&mut self, proposed: &Invoice) -> Result<Invoice> {
        let mut current = self.get(&proposed.id)?;
        ensure!(current.state != "deleted", "请先恢复已删除的发票");
        current.category = proposed.category;
        current.entry_date.clone_from(&proposed.entry_date);
        current.purchase_date.clone_from(&proposed.purchase_date);
        current.amount_cents = proposed.amount_cents;
        current.item_name = proposed.item_name.trim().into();
        current.payment_method = proposed.payment_method.trim().into();
        current.invoice_number = proposed.invoice_number.trim().into();
        current.seller_name = proposed.seller_name.trim().into();
        current.seller_tax_id = proposed.seller_tax_id.trim().into();
        current.reviewed = proposed.reviewed;
        current.duplicate_reason = proposed.duplicate_reason.trim().into();
        ensure!(
            serde_json::to_vec(&current)?.len() <= 256 * 1024,
            "元数据过大"
        );
        if current.state == "archived" {
            current.validate()?;
            self.check_archive_duplicates(&current)?;
        }
        let mut settings = self.settings()?;
        if !current.ocr_item_name.is_empty() && !current.item_name.is_empty() {
            let seller = if !current.seller_tax_id.is_empty() {
                &current.seller_tax_id
            } else {
                &current.seller_name
            };
            if !seller.is_empty() {
                let key = format!(
                    "{}\u{1f}{}",
                    seller.trim().to_uppercase(),
                    current.ocr_item_name.trim().to_uppercase()
                );
                if current.item_name == current.ocr_item_name {
                    settings.item_corrections.remove(&key);
                } else {
                    settings
                        .item_corrections
                        .insert(key, current.item_name.clone());
                }
            }
        }
        self.snapshot("edit")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            self.put(&current)?;
            self.save_settings(&settings)?;
            self.audit("edit", &current.id)?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(current)
    }
    pub fn update_ocr(&mut self, invoice: &Invoice) -> Result<()> {
        self.record_ocr(invoice, &self.settings()?)
    }
    pub fn record_ocr(&mut self, invoice: &Invoice, settings: &Settings) -> Result<()> {
        ensure!(self.get(&invoice.id)?.state == "draft", "仅草稿可更新 OCR");
        self.snapshot("ocr")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            self.put(invoice)?;
            self.save_settings(settings)?;
            self.audit("ocr", &invoice.id)?;
            Ok(())
        })();
        self.finish(result)
    }
    fn check_archive_duplicates(&self, invoice: &Invoice) -> Result<()> {
        for other in self
            .list()?
            .iter()
            .filter(|other| other.state == "archived" && other.id != invoice.id)
        {
            ensure!(
                other.blob_hash != invoice.blob_hash,
                "原始文件与归档 {} 完全重复；可保留已有归档或合并信息",
                other.id
            );
            ensure!(
                !identity_matches(other, invoice) || invoice.duplicate_reason.chars().count() >= 4,
                "发票身份与归档 {} 冲突，请核对；确需保留须填写重复理由",
                other.id
            );
        }
        Ok(())
    }
    pub fn archive(&mut self, ids: &[String]) -> Result<usize> {
        ensure!(!ids.is_empty() && ids.len() <= 2048, "请选择1至2048张发票");
        let unique: HashSet<_> = ids.iter().collect();
        ensure!(unique.len() == ids.len(), "归档选择重复");
        let mut records = Vec::new();
        for id in ids {
            let mut invoice = self.get(id)?;
            ensure!(invoice.state == "draft", "仅草稿可归档");
            invoice.validate()?;
            ensure!(invoice.reviewed, "请逐张确认已核对原件");
            self.read_invoice(id)?;
            self.check_archive_duplicates(&invoice)?;
            for previous in &records {
                let previous: &Invoice = previous;
                ensure!(
                    previous.blob_hash != invoice.blob_hash,
                    "本批次有完全重复原件"
                );
                ensure!(
                    !identity_matches(previous, &invoice)
                        || invoice.duplicate_reason.chars().count() >= 4,
                    "本批次有发票身份冲突"
                );
            }
            invoice.state = "archived".into();
            invoice.duplicate_of = None;
            records.push(invoice);
        }
        self.snapshot("archive")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            for invoice in &records {
                self.put(invoice)?;
            }
            self.audit("archive", &ids.join(","))?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(records.len())
    }
    pub fn set_category(&mut self, ids: &[String], category: Category) -> Result<usize> {
        ensure!(!ids.is_empty() && ids.len() <= 2048, "请选择发票");
        let mut records = Vec::new();
        for id in ids {
            let mut invoice = self.get(id)?;
            ensure!(invoice.state != "deleted", "不能调整已删除发票");
            invoice.category = category;
            records.push(invoice);
        }
        self.snapshot("classify")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            for invoice in &records {
                self.put(invoice)?;
            }
            self.audit("classify", &ids.join(","))?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(records.len())
    }
    pub fn delete(&mut self, ids: &[String], restore: bool) -> Result<usize> {
        ensure!(!ids.is_empty() && ids.len() <= 2048, "请选择发票");
        let mut records = Vec::new();
        for id in ids {
            let mut invoice = self.get(id)?;
            if restore {
                ensure!(invoice.state == "deleted", "仅已删除发票可恢复");
                invoice.state = invoice
                    .previous_state
                    .take()
                    .unwrap_or_else(|| "draft".into());
                if invoice.state == "archived" {
                    self.check_archive_duplicates(&invoice)?;
                }
            } else {
                ensure!(invoice.state != "deleted", "发票已删除");
                invoice.previous_state = Some(invoice.state.clone());
                invoice.state = "deleted".into();
            }
            records.push(invoice);
        }
        self.snapshot(if restore { "restore" } else { "delete" })?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            for invoice in &records {
                self.put(invoice)?;
            }
            self.audit(
                if restore { "restore" } else { "soft-delete" },
                &ids.join(","),
            )?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(records.len())
    }
    pub fn undo_batch(&mut self, batch: &str) -> Result<usize> {
        let mut records = self.list()?;
        let mut count = 0;
        for invoice in &mut records {
            if invoice.batch_id == batch && invoice.state != "deleted" {
                invoice.previous_state = Some(invoice.state.clone());
                invoice.state = "deleted".into();
                count += 1;
            } else {
                invoice.sources.retain(|s| s.batch_id != batch);
            }
        }
        self.snapshot("undo-import")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            for invoice in &records {
                self.put(invoice)?;
            }
            self.audit("undo-import", batch)?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(count)
    }
    pub fn assign_row(&mut self, job_id: &str, row_id: &str, invoice_id: &str) -> Result<Invoice> {
        let mut job = self
            .jobs()?
            .into_iter()
            .find(|j| j.id == job_id)
            .context("导入任务不存在")?;
        let index = job
            .unassigned_rows
            .iter()
            .position(|r| r.id == row_id)
            .context("待匹配行不存在")?;
        let row = job.unassigned_rows[index].clone();
        let mut invoice = self.get(invoice_id)?;
        ensure!(
            !row.container_hash.is_empty()
                && invoice.state == "draft"
                && invoice.batch_id == job_id
                && invoice
                    .sources
                    .iter()
                    .any(|s| s.container_name == row.container
                        && s.container_hash == row.container_hash),
            "只能匹配当前压缩包内的草稿；旧任务请重新导入以验证来源"
        );
        row.apply(&mut invoice);
        invoice.reviewed = false;
        invoice
            .warnings
            .push(format!("Excel 第{}行由用户匹配，请核对", row.row_number));
        job.unassigned_rows.remove(index);
        self.snapshot("match-row")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            self.put(&invoice)?;
            self.save_job(&job)?;
            self.audit("match-row", invoice_id)?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(invoice)
    }
    pub fn resolve_conflict(&mut self, candidate_id: &str, use_candidate: bool) -> Result<Invoice> {
        let mut candidate = self.get(candidate_id)?;
        ensure!(candidate.state == "draft", "仅草稿冲突可处理");
        let target_id = candidate
            .duplicate_of
            .clone()
            .context("发票没有待处理冲突")?;
        let mut existing = self.get(&target_id)?;
        ensure!(existing.state != "deleted", "冲突目标已删除，请刷新");
        if use_candidate {
            candidate.validate()?;
            ensure!(candidate.reviewed, "请先核对候选原件");
            existing.category = candidate.category;
            existing.entry_date.clone_from(&candidate.entry_date);
            existing.purchase_date.clone_from(&candidate.purchase_date);
            existing.amount_cents = candidate.amount_cents;
            existing.item_name.clone_from(&candidate.item_name);
            existing
                .payment_method
                .clone_from(&candidate.payment_method);
            existing
                .invoice_number
                .clone_from(&candidate.invoice_number);
            existing.seller_name.clone_from(&candidate.seller_name);
            existing.seller_tax_id.clone_from(&candidate.seller_tax_id);
            existing.reviewed = true;
            if existing.state == "archived" {
                self.check_archive_duplicates(&existing)?;
            }
        }
        for source in &candidate.sources {
            if !existing.sources.contains(source) {
                existing.sources.push(source.clone());
            }
        }
        candidate.previous_state = Some("draft".into());
        candidate.state = "deleted".into();
        self.snapshot("resolve")?;
        self.db.execute_batch("BEGIN IMMEDIATE")?;
        let result = (|| {
            self.put(&existing)?;
            self.put(&candidate)?;
            self.audit(
                "resolve-conflict",
                &format!("{candidate_id} -> {target_id}; use_candidate={use_candidate}"),
            )?;
            Ok(())
        })();
        self.finish(result)?;
        Ok(existing)
    }
    pub(crate) fn finish(&self, result: Result<()>) -> Result<()> {
        match result {
            Ok(()) => {
                if let Err(error) = self.db.execute_batch("COMMIT") {
                    let _ = self.db.execute_batch("ROLLBACK");
                    return Err(error.into());
                }
                Ok(())
            }
            Err(error) => {
                let _ = self.db.execute_batch("ROLLBACK");
                Err(error)
            }
        }
    }
    pub fn save_original(&self, id: &str, destination: &Path) -> Result<()> {
        let bytes = self.read_invoice(id)?;
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(destination)
            .context("目标文件已存在或无法写入，请选择新的文件名")?;
        file.write_all(&bytes)?;
        file.sync_all()?;
        Ok(())
    }
    pub fn integrity(&self) -> Result<()> {
        let check: String = self
            .db
            .query_row("PRAGMA integrity_check", [], |r| r.get(0))?;
        ensure!(check == "ok", "数据库完整性错误: {check}");
        for invoice in self.list()? {
            self.read_invoice(&invoice.id)?;
        }
        Ok(())
    }
    pub fn checkpoint(&self) -> Result<()> {
        self.db.execute_batch("PRAGMA wal_checkpoint(FULL);")?;
        Ok(())
    }
}

pub fn write_new(path: &Path, bytes: &[u8]) -> Result<()> {
    let mut file = File::options().write(true).create_new(true).open(path)?;
    file.write_all(bytes)?;
    file.sync_all()?;
    Ok(())
}
