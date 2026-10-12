# 3.0.1 验收记录

截至 2026-10-13，3.0.1 已公开发布，Windows 安装与正式端点在线升级通过。macOS Intel / Apple Silicon 构建、DMG 安装及原生存储检查通过，但缺少 Apple 发行签名与公证凭据，当前仅为未公证预览，**完整双平台正式发布目标尚未达成**。

补丁提交 `b5edb928e2c3b06d3b13345400d58901ea6bc60a` 的[完整 CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38314522740)四个任务全部通过。[PR #2](https://github.com/MIGO-OvO/LabInvoiceSystem/pull/2)合并后，`v3.0.1` 指向 `9071953d4355ea24ddb5937dae547f039fea1a7c`，树内容与该验证提交一致；[main CI](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38315853751)及[三架构签名发布](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38315867775)全部通过。

## 业务与产品验收

| 范围 | 已执行证据 | 当前状态 |
| --- | --- | --- |
| 历史 JSON、先备份复制迁移、缺日期、撤销回滚 | Rust 文件系统测试及真实 Tauri 导入 | 通过 |
| 混合多 ZIP、旧八字段 Excel、模糊人工配对 | Rust 真实 ZIP/XLSX 测试 | 通过 |
| 导入前只读预览、分类汇总、疑似重复、独立错误及取消 | Rust 文件系统测试及真实 Tauri 预览/取消/确认 UI | 通过 |
| 同根同名原件、同名 ZIP 人工匹配隔离 | 两项真实目录条目回归测试 | 通过 |
| Manifest 无损回导、原件字节、分类与日期 | Rust 测试及实际 Windows WebView 端到端验收 | 通过 |
| 原件/身份去重、编辑不可改哈希、批次原子归档、软删除恢复 | Rust 测试及桌面操作 | 通过 |
| 路径穿越、符号链接、压缩炸弹、损坏 ZIP、错误 Excel、取消/重试 | Rust 安全与恢复测试 | 通过 |
| 本地 PDF 翻页缩放、表格搜索、编辑、批量核对、分类统计、主题 | 实际 Tauri 原生窗口及 Rust 服务 | 通过 |
| 无同意 OCR 上传被拒绝 | 实际 recognize_invoice 命令，用量保持 0 | 通过 |
| 实际百度 OCR 服务端识别 | 未提供合成发票测试凭据；解析、错误与同意门槛已测试 | 未执行外部识别 |
| 更新排空、安全备份、拒绝写入与失败解锁 | 实际 Tauri 命令 | 通过 |
| Windows NSIS | 静默安装、生产页面/Rust IPC、导航、官方被动更新及自动启动、卸载，共 5 项 | 通过 |
| Windows 正式端点在线升级 | QA 引导程序 → 公开 3.0.1；真实下载、签名、安装、重启与数据保留，共 7 项 | 通过 |
| Windows/macOS 跨平台构建 | Windows x64、macOS Intel / ARM 实际构建；两个 DMG 安装、签名结构与 SQLite 启动各 3 项 | 通过；macOS 为预览 |
| 公开 Release 资产 | 13 项 SHA256、三平台 inventory、签名版本与篡改拒绝 | 通过 |
| 门户响应式、深浅主题、文字放大与下载 | 320/390/768/1440px、axe AA、200%文字、当前更新日志、实际安装包 URL | 正式域名 12 项通过 |
| macOS Apple 发行签名、公证与在线更新 | 无 Apple 凭据；ad-hoc 预览包排除于生产 latest.json | 正式分发客观阻塞 |

## 自动化与实际安装证据

- Rust core：19 项真实业务与安全测试通过；Vitest：5 项金额、日期、筛选与分组测试通过。
- Rustfmt、Clippy、TypeScript 与 Vite：完整 CI 通过。
- Windows 实际 WebView：14 个业务场景通过，归档、设置和草稿跨重启保留；只读预览取消不写入，确认后接收 ZIP；更新准备取消活动导入，保留已提交数据并可重试。
- 五个代表桌面状态在 1024×768 下 axe WCAG AA 零违规；真实滚动区域可键盘聚焦并有名称。
- Windows 生产 NSIS：5 项通过，包含官方 `/P /UPDATE /R /ARGS` 被动更新及自动启动。
- macOS Intel 与 Apple Silicon：实际 DMG 挂载、复制安装、签名结构校验、原生程序持续运行与 SQLite 初始化各 3 项通过。此检查不替代交互式 WKWebView 或用户机器的 Gatekeeper 验收。

精简原始报告保存在 [evidence/3.0.1](evidence/3.0.1/README.md)。验收只使用明确标记的合成发票，不读取用户真实归档。门户使用真实 Tauri 截图；Impeccable 构图不是软件截图。

## Windows 更新问题与公开补丁验收

3.0.0 的公开升级下载后关闭旧进程，但 NSIS 停在 `Installer Language` 对话框。实际安装包使用官方被动参数再次复现；`/UPDATE` 会跳过重新安装选择，因此安装登记版本不是原因。首次静默安装没有保存语言，`displayLanguageSelector: true` 使后续被动更新等待人工选择。

3.0.1 关闭该选项并加入真实安装回归。2026-10-13 对未发布的 `3.0.0-qa.0` 引导程序执行公开升级，使用生产公钥、正式 GitHub `latest.json` 与公开 3.0.1 NSIS，**没有测试更新服务器或替代密钥**。7 项全部通过：

1. 升级前创建并归档真实合成 PDF。
2. 官方插件发现公开 3.0.1 清单。
3. 稍后提醒持久化。
4. 跳过版本持久化，手动检查仍可更新。
5. 实际下载、签名校验、安装及生产程序自动重启。
6. 全部归档元数据、来源关联、原件字节、分类、金额及设置完全保留。
7. 更新准备生成真实数据库备份。

测试安装已卸载；本次生成的合成数据与失败诊断完整移入外部备份。3.0.0 tag 和资产保留，建议安装或升级至 [3.0.1](https://github.com/MIGO-OvO/LabInvoiceSystem/releases/tag/v3.0.1)。

公开下载后的 13 项校验和及三平台资产映射通过。Windows NSIS 与两个 macOS 更新包的原件签名、认证版本、单字节篡改拒绝及伪造版本拒绝均通过；生产清单仅含 Windows。

## 门户与仓库

[正式门户](https://migo-ovo.github.io/LabInvoiceSystem/)的[最终 Pages 工作流](https://github.com/MIGO-OvO/LabInvoiceSystem/actions/runs/38318012765)通过，三个真实安装包与 3.0.1 更新日志已上线。公开域名 12 项验收通过：实际 Release 元数据、当前更新日志、六个视口/主题的 axe AA 零违规、200%文字和三个实际下载 URL；键盘焦点、主题持久化及安装说明深链同时验证。原始结果见 [pages-live.json](evidence/3.0.1/pages-live.json)。本机 Chromium 两次瞬时 `ERR_NETWORK_CHANGED` 导航重试明确记录在报告中；其余页面和网络检查无错误。自动无障碍检查不替代完整人工屏幕阅读器测试。

[新仓库](https://github.com/MIGO-OvO/LabInvoiceSystem)与[Legacy](https://github.com/MIGO-OvO/LabInvoiceSystem-legacy)独立。重命名前后核对旧仓库 ID、refs、Tags、Release ID 和资产，另保留 mirror、bundle、Release 资产及原工作区改动备份。原工作区用户已有改动未清理。

## 剩余阻塞与验收边界

- **Apple 发行签名与公证凭据缺失**：macOS 为 ad-hoc 未公证预览，不加入生产更新清单；正式安装、Gatekeeper 与 macOS 真实在线升级仍待 Apple Secrets 配置后验证。所需字段见 [releasing.md](releasing.md)。
- **Windows 无 Authenticode 证书**：首次安装可能出现 SmartScreen 提示。已通过的 Tauri Updater 数字签名是独立机制。
- **百度 OCR 测试凭据缺失**：实际服务端识别未执行；不为测试自动上传用户发票。手工录入、解析、错误处理与未授权上传拒绝已验证。
- macOS runner 安装/启动证据不能推导出交互式 WKWebView 或所有用户设备的运行结论；需后续实际 Mac 验收。

上述阻塞解除并验证前，不将此交付标记为完整双平台正式产品目标完成。
