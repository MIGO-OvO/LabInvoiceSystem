# 3.0.1 实际验收证据

以下 JSON 从实际执行报告原样复制，全部使用合成发票。时间戳为 UTC；验收报告的日期采用 Asia/Shanghai。平台发行签名、公证与更新签名分别记录。

| 文件 | 来源与结果 |
| --- | --- |
| [desktop.json](desktop.json) | [补丁完整 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38314522740)：14 个真实 Tauri 场景，5 个桌面状态 axe AA 零违规 |
| [windows-nsis.json](windows-nsis.json) | 同一 CI：5 项真实 NSIS 安装/IPC/导航/被动更新及自动启动/卸载 |
| [macos-x64.json](macos-x64.json) | [签名 Release 工作流](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38315867775)：Intel DMG 安装、签名结构和 SQLite 启动 3 项 |
| [macos-arm64.json](macos-arm64.json) | 同一 Release 工作流：Apple Silicon 3 项；交互式 WKWebView 和 Gatekeeper 未验证 |
| [windows-public-upgrade.json](windows-public-upgrade.json) | 本机，生产公钥及 GitHub 正式端点：3.0.0-qa.0 → 公开 3.0.1，7 项通过；全部归档字段、来源、原件和设置保留 |
| [public-release.json](public-release.json) | 本机，下载公开 Release：13 项 SHA256、3 平台清单映射与正式 latest.json 一致 |
| [signatures.txt](signatures.txt) | 本机实际 release-tools 命令与输出：三平台更新包签名、认证版本、单字节篡改及伪造版本拒绝 |
| [portal-local.json](portal-local.json) | 当前门户：6 个视口/主题、200%文字与 3 个真实下载地址，共 10 项通过 |

## 公开资源

- [main 完整 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38315853751)
- [3.0.1 Release](https://github.com/MIGO-OvO/LabInvoiceSystem/releases/tag/v3.0.1)
- [release-security.json](https://github.com/MIGO-OvO/LabInvoiceSystem/releases/download/v3.0.1/release-security.json)
- [SHA256SUMS.txt](https://github.com/MIGO-OvO/LabInvoiceSystem/releases/download/v3.0.1/SHA256SUMS.txt)
- [正式 Pages](https://migo-ovo.github.io/LabInvoiceSystem/)

截图完整证据与失败诊断保存在本地外部备份；生产门户的截图在 portal/assets。macOS 未公证预览排除于生产更新清单，Windows 未配置 Authenticode。实际百度 OCR 因测试凭据缺失未执行。详细边界见 [验收记录](../../acceptance.md)。
