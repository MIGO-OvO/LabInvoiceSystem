use crate::{error, lock, AppResult, Engine};
use invoice_core::{
    model::*,
    ocr::{apply_fields, needs_automatic_ocr, OcrFields},
};
use std::{
    future::Future,
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc,
    },
    time::Duration,
};

#[derive(Debug)]
pub(crate) struct OcrInput {
    pub bytes: Vec<u8>,
    pub extension: String,
}

pub(crate) fn prepare_input(engine: &Engine, id: &str, automatic: bool) -> AppResult<OcrInput> {
    let repo = lock(&engine.repo)?;
    let invoice = repo.get(id).map_err(error)?;
    if invoice.state != "draft" {
        return Err("仅草稿可使用 OCR".into());
    }
    let settings = repo.settings().map_err(error)?;
    if !settings.cloud_ocr_consent || (automatic && !settings.automatic_ocr_consent) {
        return Err("请先明确同意将原件上传至百度 OCR".into());
    }
    let month = chrono::Local::now().format("%Y-%m").to_string();
    let usage = if settings.usage_month == month {
        settings.monthly_usage
    } else {
        0
    };
    if usage >= settings.monthly_quota {
        return Err("已达到配置的本月 OCR 限额".into());
    }
    Ok(OcrInput {
        bytes: repo.read_invoice(id).map_err(error)?,
        extension: invoice.extension,
    })
}

pub(crate) fn commit_fields(
    engine: &Engine,
    id: &str,
    fields: OcrFields,
    automatic: bool,
) -> AppResult<Invoice> {
    let mut repo = lock(&engine.repo)?;
    let mut invoice = repo.get(id).map_err(error)?;
    let mut settings = repo.settings().map_err(error)?;
    let month = chrono::Local::now().format("%Y-%m").to_string();
    if settings.usage_month != month {
        settings.usage_month = month;
        settings.monthly_usage = 0;
    }
    settings.monthly_usage = settings.monthly_usage.saturating_add(1);
    apply_fields(&mut invoice, fields, &settings, automatic);
    repo.record_ocr(&invoice, &settings).map_err(error)?;
    Ok(invoice)
}

pub(crate) async fn request<F>(future: F, cancel: Arc<AtomicBool>) -> AppResult<OcrFields>
where
    F: Future<Output = AppResult<OcrFields>> + Send + 'static,
{
    if cancel.load(Ordering::Acquire) {
        return Err("识别已取消，原件与草稿已保留".into());
    }
    // Abort only the network future. Committed invoices and the queue owner survive cancellation.
    let request = tauri::async_runtime::spawn(future);
    let abort = request.inner().abort_handle();
    let watcher = tauri::async_runtime::spawn_blocking(move || {
        while !abort.is_finished() {
            if cancel.load(Ordering::Acquire) {
                abort.abort();
                break;
            }
            std::thread::sleep(Duration::from_millis(25));
        }
    });
    let result = request.await.map_err(error)?;
    watcher.await.map_err(error)?;
    result
}

pub(crate) fn publish(
    engine: &Engine,
    job: &ImportJob,
    notify: &mut impl FnMut(&ImportJob),
) -> AppResult<()> {
    lock(&engine.repo)?.save_job(job).map_err(error)?;
    lock(&engine.jobs)?.insert(job.id.clone(), job.clone());
    notify(job);
    Ok(())
}

pub(crate) async fn run<F, Fut>(
    engine: Arc<Engine>,
    cancel: Arc<AtomicBool>,
    mut job: ImportJob,
    credentials_configured: bool,
    recognize: F,
    mut notify: impl FnMut(&ImportJob),
) -> AppResult<ImportJob>
where
    F: Fn(OcrInput) -> Fut,
    Fut: Future<Output = AppResult<OcrFields>> + Send + 'static,
{
    if job.state == "cancelled" {
        return Ok(job);
    }
    if matches!(job.state.as_str(), "queued" | "recognizing") {
        job.state = if job.failed > 0 {
            "completedWithErrors"
        } else {
            "completed"
        }
        .into();
    }
    let (settings, invoices) = {
        let repo = lock(&engine.repo)?;
        (
            repo.settings().map_err(error)?,
            job.invoice_ids
                .iter()
                .map(|id| repo.get(id).map_err(error))
                .collect::<AppResult<Vec<_>>>()?,
        )
    };
    let ids: Vec<_> = if job.ocr_retry_ids.is_empty() {
        invoices
            .iter()
            .filter(|invoice| needs_automatic_ocr(invoice))
            .map(|invoice| invoice.id.clone())
            .collect()
    } else {
        job.ocr_retry_ids
            .iter()
            .filter(|id| {
                invoices
                    .iter()
                    .any(|invoice| &invoice.id == *id && needs_automatic_ocr(invoice))
            })
            .cloned()
            .collect()
    };
    job.ocr_skipped = invoices.len() - ids.len();
    job.ocr_total = ids.len();
    job.ocr_processed = 0;
    job.ocr_success = 0;
    job.ocr_failed = 0;
    job.ocr_retry_ids = ids.clone();
    let blocked = if !settings.automatic_ocr {
        Some("自动 OCR 已关闭，草稿可手工核对或按需识别")
    } else if ids.is_empty() {
        Some("完整元数据或已识别记录已保留，无需自动 OCR")
    } else if !settings.cloud_ocr_consent || !settings.automatic_ocr_consent {
        Some("尚未授权自动上传，原件仅在本机保存")
    } else if !credentials_configured {
        Some("尚未配置百度 OCR 凭据，草稿已保留")
    } else {
        None
    };
    if let Some(reason) = blocked {
        job.ocr_message = reason.into();
        job.ocr_pending = false;
        // Keep eligible IDs so authorization/configuration can be followed by a queue retry.
        publish(&engine, &job, &mut notify)?;
        return Ok(job);
    }
    job.state = "recognizing".into();
    job.ocr_message = "逐张自动识别；完成后仍需核对与归档".into();
    publish(&engine, &job, &mut notify)?;
    for id in ids {
        if cancel.load(Ordering::Acquire) {
            break;
        }
        job.ocr_current_file = invoices
            .iter()
            .find(|invoice| invoice.id == id)
            .map(|invoice| invoice.file_name.clone())
            .unwrap_or_default();
        publish(&engine, &job, &mut notify)?;
        let result = match prepare_input(&engine, &id, true) {
            Ok(input) => request(recognize(input), cancel.clone()).await,
            Err(reason) => {
                // Consent/quota is checked before every upload. Stop without losing remaining drafts.
                job.ocr_message = reason;
                break;
            }
        };
        if cancel.load(Ordering::Acquire) {
            // A completed response is still saved if it arrived before cancellation.
            if let Ok(fields) = result {
                commit_fields(&engine, &id, fields, true)?;
                job.ocr_success += 1;
                job.ocr_processed += 1;
                job.ocr_retry_ids.retain(|candidate| candidate != &id);
            }
            break;
        }
        match result.and_then(|fields| commit_fields(&engine, &id, fields, true)) {
            Ok(_) => {
                job.ocr_success += 1;
                job.ocr_retry_ids.retain(|candidate| candidate != &id);
            }
            Err(reason) => {
                job.ocr_failed += 1;
                let source = invoices
                    .iter()
                    .find(|invoice| invoice.id == id)
                    .and_then(|invoice| invoice.sources.last());
                job.issues.push(Issue {
                    container: source.map(|s| s.container_name.clone()).unwrap_or_default(),
                    member: job.ocr_current_file.clone(),
                    message: format!("自动 OCR 未完成：{reason}；草稿保留，可重试或手工录入"),
                });
            }
        }
        job.ocr_processed += 1;
        publish(&engine, &job, &mut notify)?;
    }
    job.ocr_current_file.clear();
    job.ocr_pending = false;
    job.state = if cancel.load(Ordering::Acquire) {
        "cancelled"
    } else if job.failed > 0 || !job.ocr_retry_ids.is_empty() {
        "completedWithErrors"
    } else {
        "completed"
    }
    .into();
    if job.state == "cancelled" {
        job.ocr_message = "自动识别已取消，已完成结果和剩余草稿均已保留".into();
    } else if job.ocr_retry_ids.is_empty() {
        job.ocr_message = "自动 OCR 已完成，请逐张核对字段与分类".into();
    }
    publish(&engine, &job, &mut notify)?;
    Ok(job)
}

#[cfg(test)]
mod tests {
    use super::*;
    use invoice_core::{importing::import_paths, Repository};
    use std::sync::{atomic::AtomicUsize, Mutex};
    use tempfile::TempDir;

    fn fixture(count: usize) -> (TempDir, Arc<Engine>, ImportJob) {
        let directory = TempDir::new().unwrap();
        let mut repo = Repository::open(directory.path().join("data")).unwrap();
        let paths = (0..count)
            .map(|index| {
                let path = directory.path().join(format!("qa-{index}.pdf"));
                std::fs::write(&path, format!("%PDF-1.4\n% SYNTHETIC QA {index}\n%%EOF")).unwrap();
                path
            })
            .collect::<Vec<_>>();
        let job = import_paths(
            &mut repo,
            &paths,
            ImportJob {
                id: new_id(),
                ocr_pending: true,
                ..Default::default()
            },
            &AtomicBool::new(false),
            |_| {},
        )
        .unwrap();
        repo.save_settings(&Settings {
            cloud_ocr_consent: true,
            automatic_ocr_consent: true,
            ..Default::default()
        })
        .unwrap();
        let engine = Arc::new(Engine {
            repo: Mutex::new(repo),
            jobs: Mutex::new(Default::default()),
            cancel: Mutex::new(None),
            busy: AtomicBool::new(false),
            updating: AtomicBool::new(false),
        });
        (directory, engine, job)
    }
    fn fields() -> OcrFields {
        OcrFields {
            purchase_date: "2026-10-13".into(),
            amount_cents: 1200,
            item_name: "合成测试用品".into(),
            invoice_number: "QA-OCR".into(),
            seller_name: "合成销售方".into(),
            seller_tax_id: "QA-TAX".into(),
        }
    }

    #[test]
    fn serial_queue_persists_real_progress_and_unreviewed_drafts() {
        let (_directory, engine, job) = fixture(3);
        let calls = AtomicUsize::new(0);
        let mut progress = Vec::new();
        let result = tauri::async_runtime::block_on(run(
            engine.clone(),
            Arc::new(AtomicBool::new(false)),
            job,
            true,
            |input| {
                assert_eq!(input.extension, "pdf");
                assert!(input.bytes.starts_with(b"%PDF"));
                calls.fetch_add(1, Ordering::SeqCst);
                async { Ok(fields()) }
            },
            |job| progress.push((job.state.clone(), job.ocr_processed)),
        ))
        .unwrap();
        assert_eq!(calls.load(Ordering::SeqCst), 3);
        assert_eq!(
            (result.ocr_total, result.ocr_success, result.ocr_processed),
            (3, 3, 3)
        );
        assert_eq!(result.state, "completed");
        assert!(progress
            .iter()
            .any(|(state, count)| state == "recognizing" && *count == 1));
        let repo = lock(&engine.repo).unwrap();
        assert_eq!(repo.settings().unwrap().monthly_usage, 3);
        assert!(repo
            .list()
            .unwrap()
            .iter()
            .all(|invoice| invoice.state == "draft"
                && !invoice.reviewed
                && invoice.amount_cents == 1200));
        assert_eq!(repo.jobs().unwrap()[0].ocr_success, 3);
    }

    #[test]
    fn disabled_unconsented_or_unconfigured_queue_never_uploads() {
        for (enabled, consent, configured) in [
            (false, true, true),
            (true, false, true),
            (true, true, false),
        ] {
            let (_directory, engine, job) = fixture(1);
            let mut settings = lock(&engine.repo).unwrap().settings().unwrap();
            settings.automatic_ocr = enabled;
            settings.automatic_ocr_consent = consent;
            lock(&engine.repo)
                .unwrap()
                .save_settings(&settings)
                .unwrap();
            let result = tauri::async_runtime::block_on(run(
                engine.clone(),
                Arc::new(AtomicBool::new(false)),
                job,
                configured,
                |_| async { panic!("No upload may start") },
                |_| {},
            ))
            .unwrap();
            assert_eq!(result.ocr_success, 0);
            assert_eq!(result.state, "completed");
            assert!(!result.ocr_message.is_empty());
            assert_eq!(
                lock(&engine.repo)
                    .unwrap()
                    .settings()
                    .unwrap()
                    .monthly_usage,
                0
            );
        }
    }

    #[test]
    fn network_error_preserves_draft_and_retry_only_processes_failures() {
        let (_directory, engine, job) = fixture(2);
        let calls = AtomicUsize::new(0);
        let failed = tauri::async_runtime::block_on(run(
            engine.clone(),
            Arc::new(AtomicBool::new(false)),
            job,
            true,
            |_| {
                let first = calls.fetch_add(1, Ordering::SeqCst) == 0;
                async move {
                    if first {
                        Err("合成网络失败".into())
                    } else {
                        Ok(fields())
                    }
                }
            },
            |_| {},
        ))
        .unwrap();
        assert_eq!(
            (
                failed.ocr_success,
                failed.ocr_failed,
                failed.ocr_retry_ids.len()
            ),
            (1, 1, 1)
        );
        assert!(failed
            .issues
            .iter()
            .any(|issue| issue.message.contains("合成网络失败")));
        let result = tauri::async_runtime::block_on(run(
            engine.clone(),
            Arc::new(AtomicBool::new(false)),
            failed,
            true,
            |_| async { Ok(fields()) },
            |_| {},
        ))
        .unwrap();
        assert_eq!((result.ocr_total, result.ocr_success), (1, 1));
        assert_eq!(
            lock(&engine.repo)
                .unwrap()
                .settings()
                .unwrap()
                .monthly_usage,
            2
        );
    }

    #[test]
    fn cancellation_retains_completed_results_and_pending_ids() {
        let (_directory, engine, job) = fixture(3);
        let cancel = Arc::new(AtomicBool::new(false));
        let result = tauri::async_runtime::block_on(run(
            engine.clone(),
            cancel.clone(),
            job,
            true,
            |_| async { Ok(fields()) },
            |job| {
                if job.ocr_processed == 1 {
                    cancel.store(true, Ordering::Release);
                }
            },
        ))
        .unwrap();
        assert_eq!(result.state, "cancelled");
        assert_eq!(result.ocr_success, 1);
        assert_eq!(result.ocr_retry_ids.len(), 2);
        assert_eq!(lock(&engine.repo).unwrap().list().unwrap().len(), 3);
        assert_eq!(
            lock(&engine.repo)
                .unwrap()
                .settings()
                .unwrap()
                .monthly_usage,
            1
        );
    }

    #[test]
    fn cancellation_aborts_a_pending_network_request_promptly() {
        let cancel = Arc::new(AtomicBool::new(false));
        let flag = cancel.clone();
        let timer = std::thread::spawn(move || {
            std::thread::sleep(Duration::from_millis(50));
            flag.store(true, Ordering::Release);
        });
        let start = std::time::Instant::now();
        let result = tauri::async_runtime::block_on(request(std::future::pending(), cancel));
        timer.join().unwrap();
        assert!(result.is_err());
        assert!(start.elapsed() < Duration::from_secs(2));
    }

    #[test]
    fn quota_is_enforced_before_each_upload_including_zero_after_month_change() {
        let (_directory, engine, job) = fixture(2);
        let mut settings = lock(&engine.repo).unwrap().settings().unwrap();
        settings.monthly_quota = 1;
        lock(&engine.repo)
            .unwrap()
            .save_settings(&settings)
            .unwrap();
        let result = tauri::async_runtime::block_on(run(
            engine.clone(),
            Arc::new(AtomicBool::new(false)),
            job,
            true,
            |_| async { Ok(fields()) },
            |_| {},
        ))
        .unwrap();
        assert_eq!(result.ocr_success, 1);
        assert_eq!(result.ocr_retry_ids.len(), 1);
        assert!(result.ocr_message.contains("限额"));
        settings.monthly_quota = 0;
        settings.usage_month = "2000-01".into();
        lock(&engine.repo)
            .unwrap()
            .save_settings(&settings)
            .unwrap();
        assert!(prepare_input(&engine, &result.ocr_retry_ids[0], true)
            .unwrap_err()
            .contains("限额"));
    }

    #[test]
    fn interrupted_recognition_recovers_with_pending_ids_and_originals() {
        let (directory, engine, mut job) = fixture(2);
        job.state = "recognizing".into();
        job.ocr_retry_ids = job.invoice_ids.clone();
        lock(&engine.repo).unwrap().save_job(&job).unwrap();
        drop(engine);
        let repo = Repository::open(directory.path().join("data")).unwrap();
        let recovered = repo.jobs().unwrap();
        assert_eq!(recovered[0].state, "interrupted");
        assert_eq!(recovered[0].ocr_retry_ids.len(), 2);
        repo.integrity().unwrap();
        assert_eq!(repo.list().unwrap().len(), 2);
    }

    #[test]
    fn crash_between_parsing_and_ocr_recovers_as_unfinished() {
        let (directory, engine, job) = fixture(1);
        assert_eq!(job.state, "recognizing");
        assert!(job.ocr_pending);
        assert!(job.ocr_retry_ids.is_empty());
        drop(engine);
        let repo = Repository::open(directory.path().join("data")).unwrap();
        let recovered = &repo.jobs().unwrap()[0];
        assert_eq!(recovered.state, "interrupted");
        assert!(recovered.ocr_pending);
        assert_eq!(recovered.invoice_ids.len(), 1);
        repo.integrity().unwrap();
    }
}
