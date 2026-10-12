use anyhow::{bail, ensure, Result};
use sha2::{Digest, Sha256};
use std::{
    io::Read,
    path::{Component, Path},
    sync::atomic::{AtomicBool, Ordering},
};

pub const MAX_FILE_BYTES: u64 = 32 * 1024 * 1024;
pub const MAX_TOTAL_BYTES: u64 = 512 * 1024 * 1024;
pub const MAX_ENTRIES: usize = 2048;
pub fn digest(bytes: &[u8]) -> String {
    hex::encode(Sha256::digest(bytes))
}
pub fn digest_reader(mut reader: impl Read, cancel: &AtomicBool) -> Result<String> {
    let mut hash = Sha256::new();
    let mut buffer = [0u8; 65536];
    let mut total = 0u64;
    loop {
        cancelled(cancel)?;
        let read = reader.read(&mut buffer)?;
        if read == 0 {
            break;
        }
        total += read as u64;
        ensure!(total <= MAX_TOTAL_BYTES, "来源文件超过安全限额");
        hash.update(&buffer[..read]);
    }
    Ok(hex::encode(hash.finalize()))
}
pub fn cancelled(cancel: &AtomicBool) -> Result<()> {
    ensure!(!cancel.load(Ordering::Acquire), "操作已取消");
    Ok(())
}

pub fn safe_member(name: &str) -> Result<()> {
    ensure!(
        !name.is_empty()
            && name.len() <= 1024
            && !name.contains('\\')
            && !name.chars().any(|c| c.is_control()),
        "ZIP 文件名非法"
    );
    ensure!(
        !name.contains(':') && !name.starts_with('/') && !name.starts_with('~'),
        "ZIP 包含绝对路径或非法文件名"
    );
    for component in Path::new(name).components() {
        match component {
            Component::Normal(part) => {
                let part = part.to_string_lossy();
                ensure!(
                    !part.ends_with(['.', ' ']) && !part.contains(['<', '>', '"', '|', '?', '*']),
                    "ZIP 包含跨平台非法文件名"
                );
                let stem = part.split('.').next().unwrap_or("").to_uppercase();
                ensure!(
                    !matches!(stem.as_str(), "CON" | "PRN" | "AUX" | "NUL" | "CLOCK$")
                        && !((stem.starts_with("COM") || stem.starts_with("LPT"))
                            && stem.len() == 4
                            && stem.ends_with(['1', '2', '3', '4', '5', '6', '7', '8', '9'])),
                    "ZIP 包含设备名称"
                );
            }
            _ => bail!("ZIP 路径穿越被阻止"),
        }
    }
    ensure!(
        !name
            .split('/')
            .any(|p| p == "." || p == ".." || p.is_empty())
            || name.ends_with('/')
                && !name[..name.len() - 1]
                    .split('/')
                    .any(|p| p == "." || p == ".." || p.is_empty()),
        "ZIP 路径非法"
    );
    Ok(())
}

pub fn read_limited(
    mut reader: impl Read,
    max: u64,
    cancel: &AtomicBool,
    mut progress: impl FnMut(u64),
) -> Result<Vec<u8>> {
    let mut bytes = Vec::new();
    let mut buffer = [0u8; 65536];
    loop {
        cancelled(cancel)?;
        let read = reader.read(&mut buffer)?;
        if read == 0 {
            break;
        }
        ensure!(
            (bytes.len() as u64) + read as u64 <= max,
            "解压后文件超出安全限额"
        );
        bytes.extend_from_slice(&buffer[..read]);
        progress(read as u64);
    }
    Ok(bytes)
}

pub fn extension(name: &str) -> String {
    Path::new(name)
        .extension()
        .map(|v| v.to_string_lossy().to_lowercase())
        .unwrap_or_default()
}
pub fn is_invoice(name: &str) -> bool {
    matches!(extension(name).as_str(), "pdf" | "png" | "jpg" | "jpeg")
}
pub fn validate_file(name: &str, bytes: &[u8]) -> Result<()> {
    ensure!(
        !bytes.is_empty() && bytes.len() as u64 <= MAX_FILE_BYTES,
        "发票为空或超过32 MiB"
    );
    let valid = match extension(name).as_str() {
        "pdf" => bytes.starts_with(b"%PDF-"),
        "png" => bytes.starts_with(b"\x89PNG\r\n\x1a\n"),
        "jpg" | "jpeg" => bytes.starts_with(&[0xff, 0xd8, 0xff]),
        _ => false,
    };
    ensure!(valid, "发票扩展名与文件内容不匹配");
    Ok(())
}

pub fn validate_zip<R: Read + std::io::Seek>(zip: &mut zip::ZipArchive<R>) -> Result<u64> {
    validate_zip_names(zip, None)
}
pub fn validate_zip_names<R: Read + std::io::Seek>(
    zip: &mut zip::ZipArchive<R>,
    original_names: Option<&[String]>,
) -> Result<u64> {
    ensure!(zip.len() <= MAX_ENTRIES, "ZIP 超过2048个条目");
    let mut total = 0u64;
    let mut names = std::collections::HashSet::new();
    for index in 0..zip.len() {
        let entry = zip.by_index(index)?;
        let name = original_names
            .map(|items| items[index].as_str())
            .unwrap_or(entry.name());
        safe_member(name)?;
        ensure!(
            names.insert(name.trim_end_matches('/').to_lowercase())
                || original_names.is_some() && is_invoice(name),
            "ZIP 含同名或仅大小写不同的条目，无法安全匹配"
        );
        ensure!(
            entry
                .unix_mode()
                .map(|m| m & 0o170000 != 0o120000)
                .unwrap_or(true),
            "ZIP 符号链接被阻止"
        );
        ensure!(!entry.encrypted(), "不支持加密 ZIP");
        ensure!(entry.size() <= MAX_FILE_BYTES, "ZIP 单文件超过32 MiB");
        if entry.size() > 1024 * 1024 {
            ensure!(
                entry.compressed_size() > 0 && entry.size() / entry.compressed_size() <= 200,
                "ZIP 压缩比异常，可能是解压炸弹"
            );
        }
        total = total
            .checked_add(entry.size())
            .ok_or_else(|| anyhow::anyhow!("ZIP 大小溢出"))?;
        ensure!(total <= MAX_TOTAL_BYTES, "ZIP 解压总大小超过512 MiB");
    }
    Ok(total)
}
