use invoice_core::{
    archive::{export_zip, ExportProgress},
    importing::{import_paths, scan_legacy},
    model::*,
    Repository,
};
use serde::Serialize;
use std::{
    collections::HashMap,
    path::{Path, PathBuf},
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc, Mutex, MutexGuard,
    },
};
use tauri::{Emitter, Manager, State};
use tauri_plugin_opener::OpenerExt;

type AppResult<T> = Result<T, String>;
fn error(error: impl std::fmt::Display) -> String {
    error.to_string()
}
fn lock<T>(mutex: &Mutex<T>) -> AppResult<MutexGuard<'_, T>> {
    mutex
        .lock()
        .map_err(|_| "内部操作锁异常，请重启并校验归档".into())
}
pub struct Engine {
    repo: Mutex<Repository>,
    jobs: Mutex<HashMap<String, ImportJob>>,
    cancel: Mutex<Option<Arc<AtomicBool>>>,
    busy: AtomicBool,
    updating: AtomicBool,
}
struct Operation(Arc<Engine>);
impl Operation {
    fn enter(engine: Arc<Engine>) -> AppResult<Self> {
        if engine.updating.load(Ordering::Acquire) {
            return Err("正在安全准备更新，请稍候".into());
        }
        engine
            .busy
            .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
            .map_err(|_| "另一项发票操作正在执行，请完成或取消后重试")?;
        if engine.updating.load(Ordering::Acquire) {
            engine.busy.store(false, Ordering::Release);
            return Err("正在准备更新".into());
        }
        Ok(Self(engine))
    }
}
impl Drop for Operation {
    fn drop(&mut self) {
        self.0.busy.store(false, Ordering::Release);
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct Snapshot {
    invoices: Vec<Invoice>,
    jobs: Vec<ImportJob>,
    settings: Settings,
    statistics: Statistics,
    data_directory: String,
    version: String,
    credentials_configured: bool,
    busy: bool,
}
fn credential_entry() -> AppResult<keyring::Entry> {
    keyring::Entry::new("dev.migo.labinvoicesystem", "baidu-ocr").map_err(error)
}
fn credentials() -> AppResult<(String, String)> {
    let stored = credential_entry()?
        .get_password()
        .map_err(|_| "系统安全存储中没有可用 OCR 凭据，请重新配置")?;
    serde_json::from_str(&stored).map_err(|_| "OCR 凭据格式异常，请重新配置".into())
}

#[tauri::command]
async fn get_snapshot(state: State<'_, Arc<Engine>>) -> AppResult<Snapshot> {
    let engine = state.inner().clone();
    tauri::async_runtime::spawn_blocking(move || {
        let repo = lock(&engine.repo)?;
        let invoices = repo.list().map_err(error)?;
        let statistics = statistics(&invoices, chrono::Local::now().date_naive());
        Ok(Snapshot {
            invoices,
            jobs: repo.jobs().map_err(error)?,
            settings: repo.settings().map_err(error)?,
            statistics,
            data_directory: repo.root.to_string_lossy().into(),
            version: env!("CARGO_PKG_VERSION").into(),
            credentials_configured: credentials().is_ok(),
            busy: engine.busy.load(Ordering::Acquire),
        })
    })
    .await
    .map_err(error)?
}

#[tauri::command]
fn start_import(
    app: tauri::AppHandle,
    state: State<'_, Arc<Engine>>,
    paths: Vec<String>,
) -> AppResult<String> {
    if paths.is_empty() || paths.len() > 128 {
        return Err("请选择1至128个发票或压缩包".into());
    }
    let operation = Operation::enter(state.inner().clone())?;
    let cancel = Arc::new(AtomicBool::new(false));
    *lock(&state.cancel)? = Some(cancel.clone());
    let job = ImportJob {
        id: new_id(),
        state: "queued".into(),
        total_containers: paths.len(),
        ..Default::default()
    };
    let id = job.id.clone();
    lock(&state.jobs)?.insert(id.clone(), job.clone());
    let engine = state.inner().clone();
    let paths = paths.into_iter().map(PathBuf::from).collect::<Vec<_>>();
    tauri::async_runtime::spawn_blocking(move || {
        let result = (|| -> AppResult<ImportJob> {
            let mut repo = lock(&engine.repo)?;
            import_paths(&mut repo, &paths, job.clone(), &cancel, |progress| {
                if let Ok(mut jobs) = engine.jobs.lock() {
                    jobs.insert(progress.id.clone(), progress.clone());
                }
                let _ = app.emit("import-progress", progress);
            })
            .map_err(error)
        })();
        if let Err(reason) = result {
            let mut failed = job;
            failed.state = "failed".into();
            failed.failed = 1;
            failed.issues.push(Issue {
                container: String::new(),
                member: String::new(),
                message: reason,
            });
            if let Ok(repo) = engine.repo.lock() {
                let _ = repo.save_job(&failed);
            }
            if let Ok(mut jobs) = engine.jobs.lock() {
                jobs.insert(failed.id.clone(), failed.clone());
            }
            let _ = app.emit("import-progress", failed);
        }
        if let Ok(mut flag) = engine.cancel.lock() {
            *flag = None;
        }
        drop(operation);
        let _ = app.emit("data-changed", ());
    });
    Ok(id)
}

#[tauri::command]
fn get_job(state: State<'_, Arc<Engine>>, id: String) -> AppResult<ImportJob> {
    if let Some(job) = lock(&state.jobs)?.get(&id) {
        return Ok(job.clone());
    }
    lock(&state.repo)?
        .jobs()
        .map_err(error)?
        .into_iter()
        .find(|j| j.id == id)
        .ok_or_else(|| "导入任务不存在".into())
}
#[tauri::command]
fn cancel_operation(state: State<'_, Arc<Engine>>) -> AppResult<()> {
    if let Some(cancel) = lock(&state.cancel)?.as_ref() {
        cancel.store(true, Ordering::Release);
    }
    Ok(())
}
#[tauri::command]
fn retry_import(
    app: tauri::AppHandle,
    state: State<'_, Arc<Engine>>,
    id: String,
) -> AppResult<String> {
    let job = lock(&state.repo)?
        .jobs()
        .map_err(error)?
        .into_iter()
        .find(|j| j.id == id)
        .ok_or("任务不存在")?;
    start_import(app, state, job.input_paths)
}
#[tauri::command]
fn save_invoice(state: State<'_, Arc<Engine>>, invoice: Invoice) -> AppResult<Invoice> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?.edit(&invoice).map_err(error)
}
#[tauri::command]
fn archive_invoices(state: State<'_, Arc<Engine>>, ids: Vec<String>) -> AppResult<usize> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?.archive(&ids).map_err(error)
}
#[tauri::command]
fn classify_invoices(
    state: State<'_, Arc<Engine>>,
    ids: Vec<String>,
    category: Category,
) -> AppResult<usize> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?
        .set_category(&ids, category)
        .map_err(error)
}
#[tauri::command]
fn delete_invoices(
    state: State<'_, Arc<Engine>>,
    ids: Vec<String>,
    restore: bool,
) -> AppResult<usize> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?.delete(&ids, restore).map_err(error)
}
#[tauri::command]
fn undo_import(state: State<'_, Arc<Engine>>, id: String) -> AppResult<usize> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?.undo_batch(&id).map_err(error)
}
#[tauri::command]
fn match_excel_row(
    state: State<'_, Arc<Engine>>,
    job_id: String,
    row_id: String,
    invoice_id: String,
) -> AppResult<Invoice> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?
        .assign_row(&job_id, &row_id, &invoice_id)
        .map_err(error)
}
#[tauri::command]
fn resolve_conflict(
    state: State<'_, Arc<Engine>>,
    id: String,
    use_candidate: bool,
) -> AppResult<Invoice> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?
        .resolve_conflict(&id, use_candidate)
        .map_err(error)
}
#[tauri::command]
fn save_settings(state: State<'_, Arc<Engine>>, settings: Settings) -> AppResult<()> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?.save_settings(&settings).map_err(error)
}

#[tauri::command]
fn read_invoice_bytes(state: State<'_, Arc<Engine>>, id: String) -> AppResult<String> {
    use base64::Engine;
    let bytes = lock(&state.repo)?.read_invoice(&id).map_err(error)?;
    Ok(base64::engine::general_purpose::STANDARD.encode(bytes))
}
#[tauri::command]
fn open_original(
    app: tauri::AppHandle,
    state: State<'_, Arc<Engine>>,
    id: String,
) -> AppResult<()> {
    let repo = lock(&state.repo)?;
    repo.read_invoice(&id).map_err(error)?;
    let invoice = repo.get(&id).map_err(error)?;
    app.opener()
        .open_path(
            repo.blob_path(&invoice.blob_hash, &invoice.extension)
                .map_err(error)?
                .to_string_lossy()
                .as_ref(),
            None::<&str>,
        )
        .map_err(error)
}
#[tauri::command]
fn open_data_folder(
    app: tauri::AppHandle,
    state: State<'_, Arc<Engine>>,
    kind: String,
) -> AppResult<()> {
    let repo = lock(&state.repo)?;
    let path = match kind.as_str() {
        "exports" => invoice_core::archive::suggested_output(&repo)
            .map_err(error)?
            .parent()
            .ok_or("导出目录无效")?
            .to_owned(),
        "backups" => repo.root.join("backups"),
        _ => repo.root.clone(),
    };
    app.opener()
        .open_path(path.to_string_lossy().as_ref(), None::<&str>)
        .map_err(error)
}
#[tauri::command]
fn save_original(state: State<'_, Arc<Engine>>, id: String, path: String) -> AppResult<()> {
    let _operation = Operation::enter(state.inner().clone())?;
    lock(&state.repo)?
        .save_original(&id, Path::new(&path))
        .map_err(error)
}
#[tauri::command]
async fn export_invoices(
    app: tauri::AppHandle,
    state: State<'_, Arc<Engine>>,
    ids: Vec<String>,
    path: String,
) -> AppResult<usize> {
    let engine = state.inner().clone();
    let operation = Operation::enter(engine.clone())?;
    let cancel = Arc::new(AtomicBool::new(false));
    *lock(&engine.cancel)? = Some(cancel.clone());
    tauri::async_runtime::spawn_blocking(move || {
        let result = (|| {
            let repo = lock(&engine.repo)?;
            export_zip(
                &repo,
                &ids,
                Path::new(&path),
                &cancel,
                |progress: &ExportProgress| {
                    let _ = app.emit("export-progress", progress);
                },
            )
            .map(|m| m.invoices.len())
            .map_err(error)
        })();
        if let Ok(mut flag) = engine.cancel.lock() {
            *flag = None;
        }
        drop(operation);
        result
    })
    .await
    .map_err(error)?
}

#[tauri::command]
fn configure_credentials(
    state: State<'_, Arc<Engine>>,
    api_key: String,
    secret_key: String,
) -> AppResult<()> {
    let _operation = Operation::enter(state.inner().clone())?;
    if api_key.trim().is_empty()
        || secret_key.trim().is_empty()
        || api_key.len() > 1024
        || secret_key.len() > 1024
    {
        return Err("请填写有效的 API Key 和 Secret Key".into());
    }
    credential_entry()?
        .set_password(&serde_json::to_string(&(api_key.trim(), secret_key.trim())).map_err(error)?)
        .map_err(|_| "无法写入系统凭据存储，请检查当前用户权限".into())
}
#[tauri::command]
fn forget_credentials(state: State<'_, Arc<Engine>>) -> AppResult<()> {
    let _operation = Operation::enter(state.inner().clone())?;
    match credential_entry()?.delete_credential() {
        Ok(()) | Err(keyring::Error::NoEntry) => Ok(()),
        Err(_) => Err("无法删除系统凭据".into()),
    }
}
#[tauri::command]
async fn test_credentials() -> AppResult<()> {
    let (api, secret) = credentials()?;
    invoice_core::ocr::test_connection(&api, &secret)
        .await
        .map_err(error)
}

#[tauri::command]
async fn recognize_invoice(state: State<'_, Arc<Engine>>, id: String) -> AppResult<Invoice> {
    let engine = state.inner().clone();
    let _operation = Operation::enter(engine.clone())?;
    let (bytes, extension, consent) = {
        let repo = lock(&engine.repo)?;
        let invoice = repo.get(&id).map_err(error)?;
        if invoice.state != "draft" {
            return Err("仅草稿可使用 OCR".into());
        }
        let settings = repo.settings().map_err(error)?;
        let month = chrono::Local::now().format("%Y-%m").to_string();
        if settings.usage_month == month && settings.monthly_usage >= settings.monthly_quota {
            return Err("已达到配置的本月 OCR 限额".into());
        }
        (
            repo.read_invoice(&id).map_err(error)?,
            invoice.extension,
            settings.cloud_ocr_consent,
        )
    };
    if !consent {
        return Err("请先明确同意将当前原件上传至百度 OCR".into());
    }
    let (api, secret) = credentials()?;
    let fields = invoice_core::ocr::recognize(&bytes, &extension, &api, &secret, consent)
        .await
        .map_err(error)?;
    let mut repo = lock(&engine.repo)?;
    let mut invoice = repo.get(&id).map_err(error)?;
    let mut settings = repo.settings().map_err(error)?;
    let month = chrono::Local::now().format("%Y-%m").to_string();
    if settings.usage_month != month {
        settings.usage_month = month;
        settings.monthly_usage = 0;
    }
    settings.monthly_usage = settings.monthly_usage.saturating_add(1);
    repo.save_settings(&settings).map_err(error)?;
    invoice.purchase_date = fields.purchase_date;
    invoice.amount_cents = fields.amount_cents;
    invoice.invoice_number = fields.invoice_number;
    invoice.seller_name = fields.seller_name;
    invoice.seller_tax_id = fields.seller_tax_id;
    invoice.ocr_item_name = fields.item_name.clone();
    let seller = if !invoice.seller_tax_id.is_empty() {
        &invoice.seller_tax_id
    } else {
        &invoice.seller_name
    };
    let key = format!(
        "{}\u{1f}{}",
        seller.trim().to_uppercase(),
        fields.item_name.trim().to_uppercase()
    );
    invoice.item_name = settings
        .item_corrections
        .get(&key)
        .cloned()
        .unwrap_or(fields.item_name);
    invoice.reviewed = false;
    invoice
        .warnings
        .push("云 OCR 已完成，请核对所有字段与分类".into());
    // OCR's immutable provenance is changed only by the backend.
    repo.update_ocr(&invoice).map_err(error)?;
    Ok(invoice)
}

#[tauri::command]
fn preview_legacy(path: String) -> AppResult<Vec<String>> {
    scan_legacy(Path::new(&path))
        .map(|files| {
            files
                .iter()
                .map(|p| p.file_name().unwrap_or_default().to_string_lossy().into())
                .collect()
        })
        .map_err(error)
}
#[tauri::command]
fn import_legacy_settings(state: State<'_, Arc<Engine>>, path: String) -> AppResult<Settings> {
    let _operation = Operation::enter(state.inner().clone())?;
    let repo = lock(&state.repo)?;
    invoice_core::importing::import_legacy_settings(&repo, Path::new(&path)).map_err(error)
}
#[tauri::command]
fn audit_log(state: State<'_, Arc<Engine>>) -> AppResult<Vec<(String, String, String)>> {
    lock(&state.repo)?.audit_log().map_err(error)
}
#[tauri::command]
async fn verify_archive(state: State<'_, Arc<Engine>>) -> AppResult<()> {
    let engine = state.inner().clone();
    let operation = Operation::enter(engine.clone())?;
    tauri::async_runtime::spawn_blocking(move || {
        let result = lock(&engine.repo)?.integrity().map_err(error);
        drop(operation);
        result
    })
    .await
    .map_err(error)?
}

#[tauri::command]
async fn prepare_for_update(state: State<'_, Arc<Engine>>) -> AppResult<String> {
    let engine = state.inner().clone();
    engine
        .updating
        .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
        .map_err(|_| "已在准备更新")?;
    if let Some(cancel) = lock(&engine.cancel)?.as_ref() {
        cancel.store(true, Ordering::Release);
    }
    let result = tauri::async_runtime::spawn_blocking({
        let engine = engine.clone();
        move || {
            let deadline = std::time::Instant::now() + std::time::Duration::from_secs(60);
            while engine.busy.load(Ordering::Acquire) {
                if std::time::Instant::now() >= deadline {
                    return Err("发票操作尚未安全结束，更新已取消，请稍后重试".into());
                }
                std::thread::sleep(std::time::Duration::from_millis(25));
            }
            let repo = lock(&engine.repo)?;
            repo.checkpoint().map_err(error)?;
            repo.snapshot("before-update").map_err(error)
        }
    })
    .await
    .map_err(error)?;
    if result.is_err() {
        engine.updating.store(false, Ordering::Release);
    }
    result
}
#[tauri::command]
fn cancel_update_preparation(state: State<'_, Arc<Engine>>) {
    state.updating.store(false, Ordering::Release);
}
#[tauri::command]
fn restart_after_update(app: tauri::AppHandle, state: State<'_, Arc<Engine>>) -> AppResult<()> {
    if !state.updating.load(Ordering::Acquire) || state.busy.load(Ordering::Acquire) {
        return Err("尚未安全准备更新，拒绝重启".into());
    }
    app.restart();
}

pub fn run() {
    let context = tauri::generate_context!();
    #[cfg(windows)]
    let context = {
        let mut context = context;
        // Some WebView2 hosts omit inherited browser arguments. Forward the
        // official opt-in environment value through native options as well.
        if let Ok(arguments) = std::env::var("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS") {
            if !arguments.trim().is_empty() {
                for window in &mut context.config_mut().app.windows {
                    let defaults = window.additional_browser_args.as_deref().unwrap_or(
                        "--disable-features=msWebOOUI,msPdfOOUI,msSmartScreenProtection --autoplay-policy=no-user-gesture-required",
                    );
                    window.additional_browser_args = Some(format!("{defaults} {arguments}"));
                }
            }
        }
        context
    };
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .setup(|app| {
            let root = app.path().app_data_dir()?;
            #[cfg(debug_assertions)]
            let root = std::env::var("LABINVOICE_DATA_DIR")
                .map(PathBuf::from)
                .unwrap_or(root);
            let repo = Repository::open(root)?;
            app.manage(Arc::new(Engine {
                repo: Mutex::new(repo),
                jobs: Mutex::new(HashMap::new()),
                cancel: Mutex::new(None),
                busy: AtomicBool::new(false),
                updating: AtomicBool::new(false),
            }));
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            get_snapshot,
            start_import,
            get_job,
            cancel_operation,
            retry_import,
            save_invoice,
            archive_invoices,
            classify_invoices,
            delete_invoices,
            undo_import,
            match_excel_row,
            resolve_conflict,
            save_settings,
            read_invoice_bytes,
            open_original,
            open_data_folder,
            save_original,
            export_invoices,
            configure_credentials,
            forget_credentials,
            test_credentials,
            recognize_invoice,
            preview_legacy,
            import_legacy_settings,
            audit_log,
            verify_archive,
            prepare_for_update,
            cancel_update_preparation,
            restart_after_update
        ])
        .run(context)
        .expect("LabInvoiceSystem 启动失败");
}
