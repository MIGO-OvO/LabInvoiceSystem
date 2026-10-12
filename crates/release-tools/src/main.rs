use anyhow::{ensure, Context, Result};
use base64::{engine::general_purpose::STANDARD, Engine};
use minisign_verify::{PublicKey, Signature};
use std::{fs, path::Path};

fn verify(bytes: &[u8], signature: &str, public_key: &str, version: &str) -> Result<()> {
    let public_key = PublicKey::decode(std::str::from_utf8(&STANDARD.decode(public_key.trim())?)?)?;
    let signature = Signature::decode(std::str::from_utf8(&STANDARD.decode(signature.trim())?)?)?;
    // Same library and verification order as the official Tauri updater.
    public_key.verify(bytes, &signature, true)?;
    let signed = signature
        .trusted_comment()
        .split('\t')
        .find_map(|f| f.strip_prefix("version:"))
        .context("Signature has no authenticated version")?;
    ensure!(
        semver::Version::parse(signed.trim_start_matches('v'))?
            == semver::Version::parse(version.trim_start_matches('v'))?,
        "Authenticated version differs from manifest version"
    );
    Ok(())
}

fn main() -> Result<()> {
    let args: Vec<_> = std::env::args().collect();
    let config: serde_json::Value = serde_json::from_slice(&fs::read(
        args.get(1)
            .context("verify-update <tauri.conf.json> <version> <artifact> [artifact ...]")?,
    )?)?;
    let key = config["plugins"]["updater"]["pubkey"]
        .as_str()
        .context("Missing updater public key")?;
    let version = args.get(2).context("Missing version")?;
    ensure!(args.len() > 3, "No artifacts supplied");
    for file in args.iter().skip(3) {
        let bytes = fs::read(file)?;
        let signature = fs::read_to_string(format!("{file}.sig"))?;
        verify(&bytes, &signature, key, version)
            .with_context(|| format!("Invalid signature: {file}"))?;
        // Concrete negative cases prevent a merely syntactic signature check.
        let mut damaged = bytes.clone();
        if damaged.is_empty() {
            damaged.push(1);
        } else {
            damaged[0] ^= 1;
        }
        ensure!(
            verify(&damaged, &signature, key, version).is_err(),
            "Tampered artifact was accepted"
        );
        ensure!(
            verify(&bytes, &signature, key, "99.99.99").is_err(),
            "Tampered version was accepted"
        );
        println!(
            "Verified artifact, authenticated version, tamper rejection: {}",
            Path::new(file)
                .file_name()
                .unwrap_or_default()
                .to_string_lossy()
        );
    }
    Ok(())
}
