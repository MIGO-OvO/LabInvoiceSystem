# 3.0 验收记录

此报告随实际构建与发布继续更新，未验证的状态保持明确。最新业务提交 `1cbd17fc86da012e2b1e65995dc6568aec7cd8ef` 的[完整 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38308756211) 四个任务全部通过；[PR #1](https://github.com/MIGO-OvO/LabInvoiceSystem/pull/1) 合并后树内容与该提交一致，`v3.0.0` 指向 `7f27b45d56f2b1112fce3f41d13cc2dc27f015b9`。

| 范围 | 已执行证据 | 当前状态 |
| --- | --- | --- |
| 历史 JSON、先备份复制迁移、缺日期、撤销回滚 | Rust 文件系统测试 | 通过 |
| 混合多 ZIP、旧八字段 Excel、模糊人工配对 | Rust 真实 ZIP/XLSX 测试 | 通过 |
| 导入前只读预览、分类汇总、疑似重复、独立错误及取消 | Rust 文件系统测试 + 真实 Tauri 预览/取消/确认 UI | 本地及 CI 通过 |
| 同根同名原件、同名 ZIP 人工匹配隔离 | 两项真实目录条目回归测试 | 通过 |
| 新版无损回导、原件字节、分类与日期 | Rust 测试 + 实际 Windows WebView 端到端验收 | 通过 |
| 原件/身份去重、编辑不可改哈希、批次原子归档、软删除恢复 | Rust 测试 + 桌面操作 | 通过 |
| 路径穿越、符号链接、压缩炸弹、损坏 ZIP、错误 Excel、取消/重试 | Rust 安全与恢复测试 | 通过 |
| 本地 PDF 翻页缩放、表格搜索、编辑、批量核对、分类统计、主题 | 实际 Tauri 原生窗口及 Rust 服务 | 通过 |
| 无同意 OCR 上传被拒绝 | 实际 recognize_invoice 命令；用量保持0 | 通过 |
| 实际百度 OCR 服务端识别 | 未提供可用于合成发票验收的百度凭据；解析、错误与同意门槛已测试 | 未执行外部识别 |
| 更新排空、安全备份、拒绝写入与失败解锁 | 实际 Tauri 命令 | 通过 |
| Windows NSIS | 本地及 CI 真实安装、内嵌生产页面与 Rust IPC、导航、卸载，共4项 | 通过 |
| Windows/macOS 跨平台 CI | Windows x64、Intel 与 Apple Silicon 实际构建；两个 DMG 安装、签名结构与 SQLite 启动各3项通过 | 最新业务提交的四个任务全绿 |
| 正式 Release / 在线安装升级 | v3.0.0 三架构签名 Release 已发布；公开升级复现安装语言选择框阻塞 | 3.0.1 修复与回归验证中 |
| 门户桌面/移动、深浅、320/390/768/1440、200%文字、WCAG AA | Playwright/axe，无模拟桌面服务 | 通过首轮技术检查 |
| 门户真实下载及 Pages 部署 | 从 v3.0.0 实际 Release 资产生成；正式 Pages 11项验收通过 | 已部署，等待3.0.1同步 |
| macOS Apple 发行签名、公证 | 无 Apple 证书/公证凭据；预览包使用 ad-hoc 签名保持 bundle 完整性 | 正式分发客观阻塞 |

## 本地执行结果

- Rust core：19项真实业务与安全测试通过。
- Vitest：5项金额、日期、筛选与分组测试通过。
- Windows 实际 WebView：14个业务场景通过，五个代表状态 axe WCAG AA 无违规，重启保留归档和配置；只读预览取消不写入，确认后才真实接收 ZIP；更新准备取消正在运行的导入，保留已提交数据并可重试。
- Windows 生产 NSIS：真实安装、内嵌界面与 Rust IPC、导航、卸载，共4项通过。
- 真实 NSIS 的 `.sig`：正确原件及签名版本通过；单字节篡改和伪造版本被拒绝。
- 五个桌面状态（含 ZIP 预览）的 axe 检查固定为1024×768，覆盖热力图、原件和预览区域实际产生滚动的窗口；滚动区域可键盘聚焦并有可读取名称。
- 门户：7个视口/主题/文字缩放场景通过，axe WCAG AA 无违规，无横向溢出或未捕获脚本错误。下载资产待发布后再次检查。
- Rustfmt、Clippy、TypeScript 与 Vite：已执行；以最终 CI 结果补充。

验收只使用明确标记的合成发票，不读取用户真实归档。门户展示实际运行截图，内部 Impeccable 构图不冒充软件截图。

## CI 问题定位

Windows 运行器的 WebView2 153 已创建浏览器和渲染进程，但没有应用传到原生进程的 `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`。以进程参数、调试连接拒绝及临时原生布尔探针确认后，通过 Tauri 官方原生配置转交同一 opt-in 环境值，保留框架默认参数。临时 Rust 探针已移除；正常启动没有额外调试参数。修复后的 CI 已执行全部13个业务场景，随后暴露并修复热力图/原件滚动区域的键盘可达性问题；最终 CI 结果见发布记录。

## 公开发布后验收与补丁

`v3.0.0` 的[签名发布](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38310658889)与[main 完整 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38310602285)均全部通过。公开资产下载后重新核对13项 SHA256 与三平台 inventory；Windows NSIS 和两个 macOS 更新包的原件签名、签名版本、单字节篡改及伪造版本拒绝通过。

[正式 Pages 部署](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38312718883)通过；对公开域名执行11项检查，包含六个视口/主题的 axe AA 零违规、200%文字、三个真实安装包 URL 和未公证预览标签。

Windows 公开升级在真实下载后关闭旧进程，但 NSIS 停在 `Installer Language` 对话框。原件仍为 QA 版本；安装登记版本并未阻止更新，NSIS `/UPDATE` 会跳过重新安装选择。新回归在实际 v3.0.0 包上以 `/P /UPDATE /R /ARGS` 再次复现60秒未完成，确认语言选择配置导致无人值守阻塞。3.0.1 关闭 `displayLanguageSelector`，增加相同参数的真实安装与自动启动回归；正式在线更新状态待补丁公开升级验收后填写。失败验收的合成数据和实际备份已完整移入本地外部备份目录。

## 发布限制

缺少 Apple Developer 发行证书和公证凭据，macOS 只能提供未公证预览包；不加入生产更新清单。运行器上的 DMG 安装、原生存储初始化和持续运行检查不替代交互式 WKWebView 或用户机器的 Gatekeeper 验收。Windows 缺少 Authenticode 证书，不影响 Tauri Updater 的独立数字签名校验，但首次安装可能出现 SmartScreen 提示。
