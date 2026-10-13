# 3.1.0 实际验收证据

全部验收使用明确标记的合成发票，不包含用户真实归档或 OCR 凭据。报告时间为 UTC。

| 文件 | 来源 |
| --- | --- |
| [desktop.json](desktop.json) | [定版实际 Tauri CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38349163930)：19 场景、7 状态 axe AA 零违规；原生窗口尺寸、固定标题栏和内部滚动的实际测量 |
| [adapt-640x480.png](adapt-640x480.png) | 同一 CI 的真实 640×480 Windows WebView，滚动至核对操作；原件和文字为合成验收内容 |
| [windows-nsis.json](windows-nsis.json) | 同一 CI：NSIS 安装、生产 IPC/导航、官方被动更新及自动启动、卸载 5 项通过 |
| [macos-x64.json](macos-x64.json) | 同一 CI：Intel DMG 安装、签名结构和原生启动/SQLite 3 项通过 |
| [macos-arm64.json](macos-arm64.json) | 同一 CI：Apple Silicon 3 项通过；不替代交互式 WKWebView 或 Gatekeeper 验收 |

公开 Release 签名、正式在线升级与门户报告在对应验证完成后添加。完整边界见 [3.1.0 验收记录](../../acceptance-3.1.0.md)。
