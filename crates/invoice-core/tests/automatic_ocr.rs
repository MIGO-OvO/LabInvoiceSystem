use invoice_core::{model::*, ocr::*};

fn complete_invoice(kind: &str) -> Invoice {
    let mut invoice = Invoice::draft("batch", "qa.pdf", "hash", "pdf", 8);
    invoice.item_name = "已核对项目".into();
    invoice.purchase_date = "2026-10-12".into();
    invoice.amount_cents = 1250;
    invoice.sources.push(Source {
        metadata_kind: kind.into(),
        ..Default::default()
    });
    invoice
}
fn fields() -> OcrFields {
    OcrFields {
        purchase_date: "2026-10-13".into(),
        amount_cents: 5000,
        item_name: "OCR 项目".into(),
        invoice_number: "QA-1".into(),
        seller_name: "合成销售方".into(),
        seller_tax_id: "QA-TAX".into(),
    }
}

#[test]
fn old_settings_default_to_automatic_without_extending_upload_consent() {
    let mut old = serde_json::to_value(Settings::default()).unwrap();
    old.as_object_mut().unwrap().remove("automaticOcr");
    old.as_object_mut().unwrap().remove("automaticOcrConsent");
    old["cloudOcrConsent"] = true.into();
    let settings: Settings = serde_json::from_value(old).unwrap();
    assert!(settings.automatic_ocr);
    assert!(settings.cloud_ocr_consent);
    assert!(!settings.automatic_ocr_consent);
    let mut disabled = settings;
    disabled.automatic_ocr = false;
    assert!(
        !serde_json::from_str::<Settings>(&serde_json::to_string(&disabled).unwrap())
            .unwrap()
            .automatic_ocr
    );
}

#[test]
fn complete_metadata_and_reviewed_or_conflicting_invoices_are_preserved() {
    for kind in [
        "manifest-v1",
        "legacy-json",
        "legacy-excel",
        "legacy-migration",
        "manual-excel",
    ] {
        assert!(!needs_automatic_ocr(&complete_invoice(kind)), "{kind}");
    }
    let mut raw = complete_invoice("filename");
    assert!(needs_automatic_ocr(&raw));
    raw.reviewed = true;
    assert!(!needs_automatic_ocr(&raw));
    raw.reviewed = false;
    raw.duplicate_of = Some("existing".into());
    assert!(!needs_automatic_ocr(&raw));
}

#[test]
fn partial_metadata_is_filled_without_overwriting_known_values() {
    let mut invoice = complete_invoice("manifest-v1");
    invoice.item_name.clear();
    invoice.category = Category::LabSupplies;
    invoice.payment_method = "微信".into();
    assert!(needs_automatic_ocr(&invoice));
    apply_fields(&mut invoice, fields(), &Settings::default(), true);
    assert_eq!(invoice.amount_cents, 1250);
    assert_eq!(invoice.purchase_date, "2026-10-12");
    assert_eq!(invoice.item_name, "OCR 项目");
    assert_eq!(invoice.category, Category::LabSupplies);
    assert_eq!(invoice.payment_method, "微信");
    assert!(!invoice.reviewed);
    assert!(!needs_automatic_ocr(&invoice));
}

#[test]
fn manual_retry_refreshes_values_and_applies_correction_memory() {
    let mut invoice = complete_invoice("legacy-json");
    let mut settings = Settings::default();
    settings
        .item_corrections
        .insert("QA-TAX\u{1f}OCR 项目".into(), "校正项目".into());
    apply_fields(&mut invoice, fields(), &settings, false);
    assert_eq!(invoice.amount_cents, 5000);
    assert_eq!(invoice.item_name, "校正项目");
    assert_eq!(invoice.ocr_item_name, "OCR 项目");
    assert_eq!(
        invoice
            .warnings
            .iter()
            .filter(|w| w.as_str() == OCR_REVIEW_WARNING)
            .count(),
        1
    );
}
