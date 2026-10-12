//! ZIP 6 uses a name-keyed map and silently replaces duplicate central entries.
//! Legacy .NET ZIP exports can contain identical names. Virtual central names
//! preserve every invoice entry; the zip crate still handles decompression/CRC.
//! We never extract a member using its supplied name.
use crate::security::{cancelled, is_invoice, safe_member, MAX_ENTRIES, MAX_TOTAL_BYTES};
use anyhow::{ensure, Context, Result};
use std::{
    collections::HashSet,
    io::{Read, Seek, SeekFrom},
    sync::atomic::AtomicBool,
};

pub struct IndexedReader<R> {
    reader: R,
    patches: Vec<(u64, Vec<u8>)>,
}
impl<R: Read + Seek> Read for IndexedReader<R> {
    fn read(&mut self, buffer: &mut [u8]) -> std::io::Result<usize> {
        let position = self.reader.stream_position()?;
        let count = self.reader.read(buffer)?;
        for (start, patch) in &self.patches {
            let left = position.max(*start);
            let right = (position + count as u64).min(*start + patch.len() as u64);
            if left < right {
                buffer[(left - position) as usize..(right - position) as usize]
                    .copy_from_slice(&patch[(left - start) as usize..(right - start) as usize]);
            }
        }
        Ok(count)
    }
}
impl<R: Seek> Seek for IndexedReader<R> {
    fn seek(&mut self, position: SeekFrom) -> std::io::Result<u64> {
        self.reader.seek(position)
    }
}
fn u16_at(bytes: &[u8], offset: usize) -> u16 {
    u16::from_le_bytes([bytes[offset], bytes[offset + 1]])
}
fn u32_at(bytes: &[u8], offset: usize) -> u32 {
    u32::from_le_bytes(bytes[offset..offset + 4].try_into().expect("fixed header"))
}

pub fn indexed_zip<R: Read + Seek>(
    mut reader: R,
    cancel: &AtomicBool,
) -> Result<(zip::ZipArchive<IndexedReader<R>>, Vec<String>)> {
    let size = reader.seek(SeekFrom::End(0))?;
    ensure!(
        (22..=MAX_TOTAL_BYTES).contains(&size),
        "ZIP 大小无效或超过512 MiB"
    );
    let tail_size = size.min(65557) as usize;
    reader.seek(SeekFrom::End(-(tail_size as i64)))?;
    let mut tail = vec![0; tail_size];
    reader.read_exact(&mut tail)?;
    let end = (0..=tail.len() - 22)
        .rev()
        .find(|&i| {
            tail[i..i + 4] == *b"PK\x05\x06"
                && i + 22 + u16_at(&tail, i + 20) as usize == tail.len()
        })
        .context("ZIP 结束目录已损坏")?;
    ensure!(
        u16_at(&tail, end + 4) == 0 && u16_at(&tail, end + 6) == 0,
        "不支持分卷 ZIP"
    );
    let count = u16_at(&tail, end + 10) as usize;
    ensure!(
        count <= MAX_ENTRIES && u16_at(&tail, end + 8) as usize == count,
        "ZIP 超过2048个条目或分卷目录不一致"
    );
    let central_size = u32_at(&tail, end + 12) as u64;
    let central_start = u32_at(&tail, end + 16) as u64;
    let end_position = size - tail_size as u64 + end as u64;
    ensure!(
        central_start.checked_add(central_size) == Some(end_position),
        "ZIP 目录边界无效；不支持带前缀或 ZIP64 容器，请重新导出普通 ZIP"
    );
    reader.seek(SeekFrom::Start(central_start))?;
    let mut names = Vec::new();
    let mut patches = Vec::new();
    let mut seen = HashSet::new();
    for index in 0..count {
        cancelled(cancel)?;
        let position = reader.stream_position()?;
        ensure!(position + 46 <= end_position, "ZIP 目录截断");
        let mut header = [0; 46];
        reader.read_exact(&mut header)?;
        ensure!(header[..4] == *b"PK\x01\x02", "ZIP 目录条目无效");
        let name_size = u16_at(&header, 28) as usize;
        let extra_size = u16_at(&header, 30) as u64;
        let comment_size = u16_at(&header, 32) as u64;
        ensure!(
            name_size > 0 && name_size <= 1024 && u16_at(&header, 34) == 0,
            "ZIP 文件名过长或分卷条目"
        );
        ensure!(
            position + 46 + name_size as u64 + extra_size + comment_size <= end_position,
            "ZIP 目录字段越界"
        );
        let mut raw = vec![0; name_size];
        reader.read_exact(&mut raw)?;
        let name = std::str::from_utf8(&raw)
            .context("ZIP 名称不是 UTF-8，请使用 UTF-8 重新压缩")?
            .to_owned();
        safe_member(&name)?;
        if !seen.insert(name.trim_end_matches('/').to_lowercase()) {
            ensure!(
                is_invoice(&name),
                "ZIP 含同名元数据或目录，无法唯一确认来源"
            );
        }
        if is_invoice(&name) {
            // '~' cannot occur at the start of a validated original path.
            let virtual_name = format!("~{index:0width$x}", width = name_size - 1);
            ensure!(virtual_name.len() == name_size, "ZIP 条目索引长度错误");
            patches.push((position + 46, virtual_name.into_bytes()));
        }
        names.push(name);
        reader.seek(SeekFrom::Current((extra_size + comment_size) as i64))?;
    }
    ensure!(
        reader.stream_position()? == end_position,
        "ZIP 条目计数与目录大小不一致"
    );
    reader.seek(SeekFrom::Start(0))?;
    let archive = zip::ZipArchive::new(IndexedReader { reader, patches })?;
    ensure!(archive.len() == count, "ZIP 目录条目丢失，已停止导入");
    Ok((archive, names))
}
