#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
fn main() {
    #[cfg(debug_assertions)]
    eprintln!(
        "[DEBUG-cdp] args_present={} port_present={} data_dir_present={}",
        std::env::var("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS").is_ok(),
        std::env::var("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS")
            .is_ok_and(|value| value.contains("--remote-debugging-port=9223")),
        std::env::var("LABINVOICE_DATA_DIR").is_ok()
    );
    lab_invoice_system_lib::run()
}
