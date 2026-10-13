# 3.1.0 实际验收证据

全部验收使用明确标记的合成发票，不包含用户真实归档或 OCR 凭据。报告时间为 UTC。

| 文件 | 来源 |
| --- | --- |
| [desktop.json](desktop.json) | [定版实际 Tauri CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38349163930)：19 场景、7 状态 axe AA 零违规；原生窗口尺寸、固定标题栏和内部滚动的实际测量 |
| [adapt-640x480.png](adapt-640x480.png) | 同一 CI 的真实 640×480 Windows WebView，滚动至核对操作；原件和文字为合成验收内容 |
| [windows-nsis.json](windows-nsis.json) | 同一 CI：NSIS 安装、生产 IPC/导航、官方被动更新及自动启动、卸载 5 项通过 |
| [macos-x64.json](macos-x64.json) | 同一 CI：Intel DMG 安装、签名结构和原生启动/SQLite 3 项通过 |
| [macos-arm64.json](macos-arm64.json) | 同一 CI：Apple Silicon 3 项通过；不替代交互式 WKWebView 或 Gatekeeper 验收 |
| [public-release.json](public-release.json) | 公开 Release 下载后的 13 项 SHA256、三平台资产映射与生产 `latest.json` 一致性通过；清单仅含 Windows |
| [release-security.json](release-security.json) | [签名 Release CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38350327350) 的三平台安全清单：Updater 签名、操作系统签名与公证状态分别记录 |
| [signatures.txt](signatures.txt) | 公开下载的 Windows NSIS 和两种 macOS 更新包：原件签名、认证版本、篡改拒绝及伪造版本拒绝通过 |
| [windows-public-upgrade.json](windows-public-upgrade.json) | [正式端点升级 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38353351606)：公开 3.0.1 → 3.1.0，9 项通过；原生自动启动与后续诊断重连分别记录 |
| [after-upgrade.png](after-upgrade.png) | 同一升级 CI 的实际 3.1.0 窗口，原归档合成发票与自定义标题栏 |
| [portal-local.json](portal-local.json) | 3.1.0 门户本地 10 项通过：六种视口/主题、200% 文字与三个真实下载 URL |
| [pages-live.json](pages-live.json) | 正式域名 11 项通过，含当前版本元数据；axe AA 零违规，无横向溢出、控制台或网络错误 |

[定版 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38349163930)、[合并后 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38350165574)、[签名 Release](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38350327350)、[Pages 部署](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38351360073)及[公开在线升级](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38353351606)均成功。

升级脚本先以精确安装路径和 ProductVersion 证明新版原生进程自动启动；Explorer 未继承 WebView2 CDP 参数，因此只重启 CI 所拥有的 QA 程序后检查持久数据。macOS 未公证预览不加入生产更新清单。完整边界见 [3.1.0 验收记录](../../acceptance-3.1.0.md)。
