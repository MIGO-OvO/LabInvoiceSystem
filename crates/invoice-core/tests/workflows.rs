use invoice_core::{archive::export_zip, importing::*, model::*, security::*, Repository};
use std::{
    fs::{self, File},
    io::{Cursor, Write},
    path::{Path, PathBuf},
    sync::atomic::{AtomicBool, Ordering},
};
use tempfile::TempDir;
fn pdf(value: &str) -> Vec<u8> {
    format!("%PDF-1.4\n% Synthetic QA invoice {value}\n1 0 obj << /Type /Catalog >> endobj\n%%EOF")
        .into_bytes()
}
fn zip_file(path: &Path, entries: Vec<(&str, Vec<u8>)>) {
    let mut zip = zip::ZipWriter::new(File::create(path).unwrap());
    for (name, bytes) in entries {
        zip.start_file(name, zip::write::SimpleFileOptions::default())
            .unwrap();
        zip.write_all(&bytes).unwrap();
    }
    zip.finish().unwrap();
}
fn import(repo: &mut Repository, paths: &[PathBuf]) -> ImportJob {
    import_paths(
        repo,
        paths,
        ImportJob {
            id: new_id(),
            ..Default::default()
        },
        &AtomicBool::new(false),
        |_| {},
    )
    .unwrap()
}
fn complete(repo: &mut Repository, id: &str, category: Category, amount: i64) {
    let mut invoice = repo.get(id).unwrap();
    invoice.item_name = "合成验收用品".into();
    invoice.entry_date = "2026-10-12".into();
    invoice.purchase_date = "2026-10-10".into();
    invoice.amount_cents = amount;
    invoice.category = category;
    invoice.reviewed = true;
    repo.edit(&invoice).unwrap();
}
fn excel(rows: &[(&str, &str, &str)]) -> Vec<u8> {
    let mut book = rust_xlsxwriter::Workbook::new();
    let sheet = book.add_worksheet();
    let headers = [
        "发票录入日期",
        "购买日期",
        "金额",
        "项目名称",
        "支付方式",
        "发票号码",
        "销售方名称",
        "销售方税号",
    ];
    for (c, h) in headers.iter().enumerate() {
        sheet.write_string(0, c as u16, *h).unwrap();
    }
    for (index, (item, amount, number)) in rows.iter().enumerate() {
        let row = (index + 1) as u32;
        for (c, v) in [
            (0, "2026-10-12"),
            (1, "2026-10-10"),
            (2, *amount),
            (3, *item),
            (4, "公务卡"),
            (5, *number),
            (6, "合成销售方"),
            (7, "QA-TAX"),
        ] {
            sheet.write_string(row, c, v).unwrap();
        }
    }
    book.save_to_buffer().unwrap()
}

#[test]
fn exact_cents_and_dates_do_not_guess() {
    assert_eq!(amount_to_cents("￥1,234.50").unwrap(), 123450);
    for value in ["1.001", "-1", "NaN", "1e9", "99999999999", "foo"] {
        assert!(amount_to_cents(value).is_err(), "{value}");
    }
    assert_eq!(
        normalize_date("2025-11-12T00:00:00"),
        Some("2025-11-12".into())
    );
    assert_eq!(normalize_date("bad"), None);
}

#[test]
fn legacy_migration_keeps_originals_backup_and_undo() {
    let temp = TempDir::new().unwrap();
    let old = temp.path().join("legacy/2025-11");
    fs::create_dir_all(&old).unwrap();
    let source = old.join("20251112-电动机-公务卡-117.60元.pdf");
    let original = pdf("legacy");
    fs::write(&source, &original).unwrap();
    let metadata = serde_json::json!({"InvoiceDate":"2025-11-12T00:00:00","EntryDate":"2025-11-20T00:00:00","Amount":117.60,"ItemName":"电动机","PaymentMethod":"公务卡","InvoiceNumber":"000123","SellerName":"QA","SellerTaxId":"QA-1"});
    fs::write(source.with_extension("json"), metadata.to_string()).unwrap();
    let root = temp.path().join("v3");
    let mut repo = Repository::open(&root).unwrap();
    let job = import(&mut repo, &[temp.path().join("legacy")]);
    assert_eq!(job.success, 1);
    let invoice = repo.list().unwrap().remove(0);
    assert_eq!(invoice.category, Category::Uncategorized);
    assert_eq!(invoice.amount_cents, 11760);
    assert_eq!(invoice.entry_date, "2025-11-20");
    assert_eq!(invoice.invoice_number, "000123");
    assert_eq!(fs::read(&source).unwrap(), original);
    assert!(fs::read_dir(root.join("backups")).unwrap().any(|e| e
        .unwrap()
        .path()
        .extension()
        .is_some_and(|s| s == "zip")));
    assert_eq!(repo.undo_batch(&job.id).unwrap(), 1);
    assert_eq!(repo.get(&invoice.id).unwrap().state, "deleted");
    assert_eq!(fs::read(source).unwrap(), original);
    repo.integrity().unwrap();
}

#[test]
fn legacy_missing_entry_date_requires_review() {
    let temp = TempDir::new().unwrap();
    let source = temp.path().join("legacy.pdf");
    fs::write(&source, pdf("missing date")).unwrap();
    fs::write(
        source.with_extension("json"),
        r#"{"InvoiceDate":"2025-11-12","Amount":10,"ItemName":"样品","PaymentMethod":"现金"}"#,
    )
    .unwrap();
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    import(&mut repo, &[source]);
    let mut invoice = repo.list().unwrap().remove(0);
    assert!(invoice.entry_date.is_empty());
    invoice.reviewed = true;
    repo.edit(&invoice).unwrap();
    assert!(repo.archive(&[invoice.id]).is_err());
}

#[test]
fn multiple_zip_dedup_same_names_statistics_and_roundtrip() {
    let temp = TempDir::new().unwrap();
    let a = temp.path().join("a.zip");
    let b = temp.path().join("b.zip");
    zip_file(
        &a,
        vec![
            ("one/invoice.pdf", pdf("one")),
            ("two/invoice.pdf", pdf("two")),
        ],
    );
    zip_file(
        &b,
        vec![("invoice.pdf", pdf("one")), ("third.pdf", pdf("three"))],
    );
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[a, b.clone()]);
    assert_eq!(job.success, 3);
    assert_eq!(job.duplicates, 1);
    assert_eq!(job.processed_containers, 2);
    let ids = repo
        .list()
        .unwrap()
        .iter()
        .map(|i| i.id.clone())
        .collect::<Vec<_>>();
    for (index, id) in ids.iter().enumerate() {
        complete(
            &mut repo,
            id,
            if index % 2 == 0 {
                Category::OfficeLiving
            } else {
                Category::LabSupplies
            },
            101 + index as i64,
        );
    }
    repo.archive(&ids).unwrap();
    let stats = statistics(
        &repo.list().unwrap(),
        chrono::NaiveDate::from_ymd_opt(2026, 10, 13).unwrap(),
    );
    assert_eq!(stats.total.amount_cents, 306);
    assert_eq!(stats.last30_cents, 306);
    assert_eq!(stats.categories[&Category::OfficeLiving].amount_cents, 204);
    let output = temp.path().join("export.zip");
    let manifest = export_zip(&repo, &ids, &output, &AtomicBool::new(false), |_| {}).unwrap();
    assert_eq!(manifest.invoices.len(), 3);
    assert!(export_zip(&repo, &ids, &output, &AtomicBool::new(false), |_| {}).is_err());
    let mut receiver = Repository::open(temp.path().join("receiver")).unwrap();
    let received = import(&mut receiver, std::slice::from_ref(&output));
    assert_eq!(received.success, 3);
    assert!(received.unassigned_rows.is_empty());
    let result = receiver.list().unwrap();
    for original in repo.list().unwrap() {
        let reimport = result
            .iter()
            .find(|i| i.blob_hash == original.blob_hash)
            .unwrap();
        assert_eq!(reimport.amount_cents, original.amount_cents);
        assert_eq!(reimport.category, original.category);
        assert_eq!(reimport.entry_date, original.entry_date);
        assert_eq!(reimport.purchase_date, original.purchase_date);
        assert_eq!(reimport.invoice_number, original.invoice_number);
        assert_eq!(reimport.file_name, original.file_name);
        assert_eq!(
            receiver.read_invoice(&reimport.id).unwrap(),
            repo.read_invoice(&original.id).unwrap()
        );
        assert!(reimport.sources.len() > original.sources.len());
    }
    let again = import(&mut receiver, &[output]);
    assert_eq!(again.success, 0);
    assert_eq!(again.duplicates, 3);
    receiver.integrity().unwrap();
}

#[test]
fn legacy_excel_unique_mapping_and_ambiguous_rows() {
    let temp = TempDir::new().unwrap();
    let path = temp.path().join("old.zip");
    zip_file(
        &path,
        vec![
            ("20261010-样品-公务卡-12.50元.pdf", pdf("legacy excel")),
            ("明细.xlsx", excel(&[("样品", "12.50", "0001")])),
        ],
    );
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let result = import(&mut repo, &[path]);
    assert!(result.unassigned_rows.is_empty());
    let invoice = repo.list().unwrap().remove(0);
    assert_eq!(invoice.invoice_number, "0001");
    assert_eq!(invoice.amount_cents, 1250);
    assert_eq!(invoice.category, Category::Uncategorized);
    let ambiguous = temp.path().join("ambiguous.zip");
    zip_file(
        &ambiguous,
        vec![
            ("a/20261010-试剂-公务卡-20元.pdf", pdf("A")),
            ("b/20261010-试剂-公务卡-20元.pdf", pdf("B")),
            (
                "明细.xlsx",
                excel(&[("试剂", "20", "0002"), ("试剂", "20", "0003")]),
            ),
        ],
    );
    let job = import(&mut repo, &[ambiguous]);
    assert_eq!(job.unassigned_rows.len(), 2);
    let target = repo
        .list()
        .unwrap()
        .into_iter()
        .find(|i| i.batch_id == job.id)
        .unwrap();
    assert!(target.invoice_number.is_empty());
    let assigned = repo
        .assign_row(&job.id, &job.unassigned_rows[0].id, &target.id)
        .unwrap();
    assert_eq!(assigned.invoice_number, "0002");
    assert!(!assigned.reviewed);
}

#[test]
fn corruption_bad_excel_and_package_atomicity() {
    let temp = TempDir::new().unwrap();
    let bad = temp.path().join("bad.zip");
    fs::write(&bad, b"not a zip").unwrap();
    let invalid = temp.path().join("invalid.zip");
    zip_file(
        &invalid,
        vec![
            ("valid.pdf", pdf("valid")),
            ("invalid.png", b"not png".to_vec()),
        ],
    );
    let excel_bad = temp.path().join("excel.zip");
    zip_file(
        &excel_bad,
        vec![
            ("invoice.pdf", pdf("manual")),
            ("bad.xlsx", b"not excel".to_vec()),
        ],
    );
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[bad, invalid, excel_bad]);
    assert_eq!(job.failed, 2);
    assert_eq!(job.success, 1);
    assert_eq!(repo.list().unwrap().len(), 1);
    assert!(job.issues.iter().any(|e| e.member == "bad.xlsx"));
    repo.integrity().unwrap();
}

#[test]
fn zip_slip_symlink_bomb_and_names_are_rejected() {
    for path in [
        "../x.pdf",
        "/x.pdf",
        "C:/x.pdf",
        "a\\x.pdf",
        "CON.pdf",
        "a/../x.pdf",
        "x. /a.pdf",
        "a//b.pdf",
    ] {
        assert!(safe_member(path).is_err(), "{path}");
    }
    let temp = TempDir::new().unwrap();
    let slip = temp.path().join("slip.zip");
    zip_file(&slip, vec![("../escape.pdf", pdf("escape"))]);
    let bomb = temp.path().join("bomb.zip");
    let mut writer = zip::ZipWriter::new(File::create(&bomb).unwrap());
    writer
        .start_file(
            "bomb.pdf",
            zip::write::SimpleFileOptions::default()
                .compression_method(zip::CompressionMethod::Deflated),
        )
        .unwrap();
    writer.write_all(&vec![0u8; 2 * 1024 * 1024]).unwrap();
    writer.finish().unwrap();
    let symlink = temp.path().join("symlink.zip");
    let mut writer = zip::ZipWriter::new(File::create(&symlink).unwrap());
    writer
        .add_symlink(
            "link.pdf",
            "../outside",
            zip::write::SimpleFileOptions::default(),
        )
        .unwrap();
    writer.finish().unwrap();
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let result = import(&mut repo, &[slip, bomb, symlink]);
    assert_eq!(result.failed, 3);
    assert!(repo.list().unwrap().is_empty());
    assert!(!temp.path().join("escape.pdf").exists());
}

#[test]
fn cancel_and_retry_resume_without_partial_package() {
    let temp = TempDir::new().unwrap();
    let path = temp.path().join("cancel.zip");
    zip_file(&path, vec![("a.pdf", pdf("A")), ("b.pdf", pdf("B"))]);
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let cancel = AtomicBool::new(false);
    let job = import_paths(
        &mut repo,
        std::slice::from_ref(&path),
        ImportJob {
            id: new_id(),
            ..Default::default()
        },
        &cancel,
        |p| {
            if p.processed_files >= 1 {
                cancel.store(true, Ordering::Release)
            }
        },
    )
    .unwrap();
    assert_eq!(job.state, "cancelled");
    assert!(repo.list().unwrap().is_empty());
    let retry = import(&mut repo, &[path]);
    assert_eq!(retry.success, 2);
    repo.integrity().unwrap();
}

#[test]
fn archive_batch_is_atomic_edit_is_immutable_soft_delete_restores() {
    let temp = TempDir::new().unwrap();
    let a = temp.path().join("a.pdf");
    let b = temp.path().join("b.pdf");
    fs::write(&a, pdf("A")).unwrap();
    fs::write(&b, pdf("B")).unwrap();
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[a, b]);
    let ids = job.invoice_ids;
    complete(&mut repo, &ids[0], Category::OfficeLiving, 100);
    assert!(repo.archive(&ids).is_err());
    assert_eq!(repo.get(&ids[0]).unwrap().state, "draft");
    complete(&mut repo, &ids[1], Category::LabSupplies, 200);
    repo.archive(&ids).unwrap();
    let mut edit = repo.get(&ids[0]).unwrap();
    let hash = edit.blob_hash.clone();
    edit.blob_hash = "../attack".into();
    edit.state = "deleted".into();
    edit.entry_date = "2026-09-01".into();
    let saved = repo.edit(&edit).unwrap();
    assert_eq!(saved.blob_hash, hash);
    assert_eq!(saved.state, "archived");
    assert_eq!(saved.entry_date, "2026-09-01");
    repo.set_category(&ids, Category::LabSupplies).unwrap();
    repo.delete(&ids, false).unwrap();
    assert_eq!(
        statistics(&repo.list().unwrap(), chrono::Local::now().date_naive())
            .total
            .count,
        0
    );
    repo.delete(&ids, true).unwrap();
    repo.integrity().unwrap();
    assert_eq!(repo.get(&ids[0]).unwrap().state, "archived");
}

#[test]
fn tampered_manifest_does_not_fallback_and_restore_cannot_duplicate() {
    let temp = TempDir::new().unwrap();
    let source = temp.path().join("a.pdf");
    fs::write(&source, pdf("signed hash")).unwrap();
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, std::slice::from_ref(&source));
    let id = job.invoice_ids[0].clone();
    complete(&mut repo, &id, Category::OfficeLiving, 100);
    repo.archive(std::slice::from_ref(&id)).unwrap();
    let out = temp.path().join("good.zip");
    let mut manifest = export_zip(
        &repo,
        std::slice::from_ref(&id),
        &out,
        &AtomicBool::new(false),
        |_| {},
    )
    .unwrap();
    let mut original = zip::ZipArchive::new(File::open(&out).unwrap()).unwrap();
    let mut bytes = Vec::new();
    use std::io::Read;
    original
        .by_name(&manifest.invoices[0].path)
        .unwrap()
        .read_to_end(&mut bytes)
        .unwrap();
    manifest.invoices[0].sha256 = "0".repeat(64);
    let bad = temp.path().join("tampered.zip");
    zip_file(
        &bad,
        vec![
            (&manifest.invoices[0].path, bytes),
            ("manifest.json", serde_json::to_vec(&manifest).unwrap()),
        ],
    );
    let result = import(&mut repo, &[bad]);
    assert_eq!(result.failed, 1);
    assert_eq!(repo.list().unwrap().len(), 1);
    repo.delete(std::slice::from_ref(&id), false).unwrap();
    let again = import(&mut repo, &[source]);
    let replacement = again.invoice_ids[0].clone();
    complete(&mut repo, &replacement, Category::OfficeLiving, 100);
    repo.archive(&[replacement]).unwrap();
    assert!(repo.delete(&[id], true).is_err());
}

#[test]
fn identity_conflicts_and_explicit_merge_preserve_original() {
    let temp = TempDir::new().unwrap();
    let old = temp.path().join("old.pdf");
    let new = temp.path().join("new.pdf");
    for (path, body) in [(&old, "old"), (&new, "new")] {
        fs::write(path, pdf(body)).unwrap();
        fs::write(path.with_extension("json"),r#"{"EntryDate":"2026-10-12","InvoiceDate":"2026-10-10","Amount":10,"ItemName":"样品","PaymentMethod":"现金","InvoiceNumber":"0001","SellerTaxId":"TAX"}"#).unwrap();
    }
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let first = import(&mut repo, &[old]);
    let first_id = first.invoice_ids[0].clone();
    let mut original = repo.get(&first_id).unwrap();
    original.reviewed = true;
    repo.edit(&original).unwrap();
    repo.archive(std::slice::from_ref(&first_id)).unwrap();
    let second = import(&mut repo, &[new]);
    assert_eq!(second.duplicates, 1);
    let candidate = repo.get(&second.invoice_ids[0]).unwrap();
    assert_eq!(candidate.duplicate_of.as_deref(), Some(first_id.as_str()));
    assert!(repo.archive(std::slice::from_ref(&candidate.id)).is_err());
    let merged = repo.resolve_conflict(&candidate.id, false).unwrap();
    assert_eq!(merged.blob_hash, original.blob_hash);
    assert_eq!(merged.sources.len(), 2);
    assert_eq!(repo.get(&candidate.id).unwrap().state, "deleted");
}

#[test]
fn reopen_preserves_drafts_and_marks_interrupted_jobs() {
    let temp = TempDir::new().unwrap();
    let source = temp.path().join("a.pdf");
    fs::write(&source, pdf("restart")).unwrap();
    let root = temp.path().join("data");
    let id;
    {
        let mut repo = Repository::open(&root).unwrap();
        let job = import(&mut repo, &[source]);
        id = job.invoice_ids[0].clone();
        complete(&mut repo, &id, Category::LabSupplies, 101);
        repo.save_job(&ImportJob {
            id: "interrupted".into(),
            state: "running".into(),
            ..Default::default()
        })
        .unwrap();
    }
    let repo = Repository::open(root).unwrap();
    assert_eq!(repo.get(&id).unwrap().amount_cents, 101);
    assert_eq!(repo.jobs().unwrap()[0].state, "interrupted");
    repo.integrity().unwrap();
}

#[test]
fn ocr_fields_preserve_old_spec_rules_and_strings() {
    let response = serde_json::json!({"words_result":{"InvoiceDate":[{"word":"2025年11月12日"}],"AmountInFiguers":{"word":"¥117.60"},"CommodityName":[{"word":"*电机"},{"word":"*电缆"}],"CommodityType":[{"word":"10件"},{"word":"M10"}],"InvoiceNum":"000123","SellerName":"QA","SellerRegisterNum":"QA-TAX"}});
    let fields = invoice_core::ocr::parse_response(&response).unwrap();
    assert_eq!(fields.amount_cents, 11760);
    assert_eq!(fields.purchase_date, "2025-11-12");
    assert_eq!(fields.item_name, "电机, M10");
    assert_eq!(fields.invoice_number, "000123");
    assert!(invoice_core::ocr::parse_response(&serde_json::json!({"error_code":17})).is_err());
}

#[test]
fn last30_excludes_future_and_30th_day() {
    let temp = TempDir::new().unwrap();
    let path = temp.path().join("a.pdf");
    fs::write(&path, pdf("stats")).unwrap();
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[path]);
    complete(&mut repo, &job.invoice_ids[0], Category::Uncategorized, 100);
    let mut invoice = repo.get(&job.invoice_ids[0]).unwrap();
    invoice.state = "archived".into();
    let date = chrono::NaiveDate::from_ymd_opt(2026, 10, 13).unwrap();
    let samples = [0, 29, 30, -1].map(|days| {
        let mut i = invoice.clone();
        i.purchase_date = (date - chrono::Duration::days(days)).to_string();
        i
    });
    assert_eq!(statistics(&samples, date).last30_cents, 200);
}

#[test]
fn malicious_excel_is_bounded_before_parsing() {
    let mut writer = zip::ZipWriter::new(Cursor::new(Vec::new()));
    writer
        .start_file("../worksheet.xml", zip::write::SimpleFileOptions::default())
        .unwrap();
    writer.write_all(b"<data/>").unwrap();
    let bytes = writer.finish().unwrap().into_inner();
    let temp = TempDir::new().unwrap();
    let path = temp.path().join("excel.zip");
    zip_file(
        &path,
        vec![("invoice.pdf", pdf("invoice")), ("bad.xlsx", bytes)],
    );
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[path]);
    assert_eq!(job.success, 1);
    assert!(job.issues.iter().any(|i| i.message.contains("穿越")));
}

#[test]
fn legacy_identical_member_names_preserve_both_originals() {
    let temp = TempDir::new().unwrap();
    let file = temp.path().join("legacy-same-name.zip");
    zip_file(
        &file,
        vec![
            ("a.pdf", pdf("first same-name invoice")),
            ("b.pdf", pdf("second same-name invoice")),
        ],
    );
    // Equal-length name substitution produces the .NET duplicate-name layout.
    let mut bytes = fs::read(&file).unwrap();
    let positions = bytes
        .windows(5)
        .enumerate()
        .filter_map(|(i, part)| (part == b"b.pdf").then_some(i))
        .collect::<Vec<_>>();
    assert_eq!(positions.len(), 2);
    for index in positions {
        bytes[index..index + 5].copy_from_slice(b"a.pdf");
    }
    fs::write(&file, bytes).unwrap();
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[file]);
    assert_eq!(job.success, 2);
    assert_eq!(job.failed, 0);
    let invoices = repo.list().unwrap();
    assert_eq!(invoices[0].file_name, invoices[1].file_name);
    assert_ne!(invoices[0].blob_hash, invoices[1].blob_hash);
    assert_ne!(
        invoices[0].sources[0].member_index,
        invoices[1].sources[0].member_index
    );
    let originals = invoices
        .iter()
        .map(|i| repo.read_invoice(&i.id).unwrap())
        .collect::<Vec<_>>();
    assert!(originals.contains(&pdf("first same-name invoice")));
    assert!(originals.contains(&pdf("second same-name invoice")));
    repo.integrity().unwrap();
}

#[test]
fn manual_excel_pairing_cannot_cross_same_named_zip_containers() {
    let temp = TempDir::new().unwrap();
    let left = temp.path().join("left/submission.zip");
    let right = temp.path().join("right/submission.zip");
    fs::create_dir_all(left.parent().unwrap()).unwrap();
    fs::create_dir_all(right.parent().unwrap()).unwrap();
    zip_file(
        &left,
        vec![
            ("invoice.pdf", pdf("left")),
            ("明细.xlsx", excel(&[("样品", "20", "LEFT")])),
        ],
    );
    zip_file(
        &right,
        vec![
            ("invoice.pdf", pdf("right")),
            ("明细.xlsx", excel(&[("样品", "30", "RIGHT")])),
        ],
    );
    let mut repo = Repository::open(temp.path().join("data")).unwrap();
    let job = import(&mut repo, &[left, right]);
    assert_eq!(job.unassigned_rows.len(), 2);
    let row = &job.unassigned_rows[0];
    let invoices = repo.list().unwrap();
    let matching = invoices
        .iter()
        .find(|i| i.sources[0].container_hash == row.container_hash)
        .unwrap();
    let other = invoices
        .iter()
        .find(|i| i.sources[0].container_hash != row.container_hash)
        .unwrap();
    assert!(repo.assign_row(&job.id, &row.id, &other.id).is_err());
    assert_eq!(
        repo.assign_row(&job.id, &row.id, &matching.id)
            .unwrap()
            .invoice_number,
        row.invoice_number
    );
}
