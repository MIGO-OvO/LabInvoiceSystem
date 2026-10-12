use anyhow::{Context, Result};
use invoice_core::{importing::import_paths, model::*, Repository};
use std::{path::PathBuf, sync::atomic::AtomicBool};
fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().collect();
    let root = args.get(1).context(
        "usage: invoice-cli <data-directory> list|import|archive|verify|export [files/ids]",
    )?;
    let mut repo = Repository::open(root)?;
    match args.get(2).map(String::as_str).unwrap_or("list") {
        "list" => println!("{}", serde_json::to_string_pretty(&repo.list()?)?),
        "import" => {
            let paths = args.iter().skip(3).map(PathBuf::from).collect::<Vec<_>>();
            let job = import_paths(
                &mut repo,
                &paths,
                ImportJob {
                    id: new_id(),
                    ..Default::default()
                },
                &AtomicBool::new(false),
                |_| {},
            )?;
            println!("{}", serde_json::to_string_pretty(&job)?);
        }
        "archive" => {
            let ids: Vec<_> = args.iter().skip(3).cloned().collect();
            for id in &ids {
                let mut invoice = repo.get(id)?;
                invoice.reviewed = true;
                repo.edit(&invoice)?;
            }
            println!("{}", repo.archive(&ids)?);
        }
        "verify" => {
            repo.integrity()?;
            println!("Database and original file hashes verified");
        }
        "export" => {
            let output = args.get(3).context("export requires output ZIP path")?;
            let ids = args.iter().skip(4).cloned().collect::<Vec<_>>();
            let manifest = invoice_core::archive::export_zip(
                &repo,
                &ids,
                std::path::Path::new(output),
                &AtomicBool::new(false),
                |_| {},
            )?;
            println!("Exported {} verified originals", manifest.invoices.len());
        }
        _ => anyhow::bail!("Unknown command"),
    }
    Ok(())
}
