# 3.0 实施计划与阶段状态

## 授权与工作边界

用户授权独立重构、备份、重命名旧仓库、新建仓库、提交推送、合并、Pages 与 Release。旧工作区的未提交 PRODUCT.md、已跟踪构建产物及其变更保持原样。新代码在独立 LabInvoiceSystem-v3 目录开发。备份在相邻 `.labinvoice-backups/20261013-v3`，不提交。

## 分阶段执行

1. **审计：完成。** 记录旧业务、字段、归档关系和 OCR 规则；保留本地 bundle、远端 mirror、原工作区 binary diff、未提交产品文档、仓库/Release 元数据与全部 Release 资产。重命名后核对仓库 ID、refs、Tags、Release ID 和资产。
2. **规划：完成。** Rust core 与 Tauri commands；SQLite 事务元数据、SHA-256 不可变原件、持久化导入队列、归档前核对、软删除恢复、Manifest v1、整数分金额和系统凭据存储。
3. **实施：完成。** 连接多 ZIP、Excel、历史迁移、去重、分类统计、OCR、PDF、React 工作流、更新安全锁、跨平台 CI 和独立门户。
4. **验证：可执行部分完成。** Rust 19 项、Vitest 5 项、真实 Tauri 14 场景、5 个桌面状态 axe AA、Windows NSIS 5 项和两个 macOS DMG 各 3 项通过。公开资产校验、签名/认证版本/篡改反例和 Windows 公开升级 7 项通过。线上百度识别和真实 Mac 交互仍未验证。
5. **发布：Windows 完成；macOS 正式分发阻塞。** 独立开发分支经 CI 合并 main，发布 v3.0.0；实际升级发现语言选择阻塞后修复并发布 v3.0.1。三架构 Release 和 Pages 已公开，macOS 明确标记未公证预览并排除于生产 updater。
6. **交付：报告与证据已整理。** 提供功能矩阵、实际安装/升级报告、CI、Pages 和安装包链接。新工作区清理本次验收临时文件；不改动旧工作区用户已有变更。完整双平台目标等待 Apple 凭据及对应实际验收。

## Impeccable 执行

实际使用已安装的 4.0.4 SKILL.md，并运行 `context.mjs --target portal/index.html`。init 产品事实来自用户 brief 与旧版代码；用户特别授权自主完成设计选择。桌面保留旧导航与核对习惯，门户独立设计。shape/craft、layout/typeset、双独立 critique、polish、audit、finish 和 documenter 均已执行，决策、评分和限制见 [design-review.md](design-review.md)。真实 Tauri 截图使用合成数据。3.0.1 仅补充实际发布元数据和更新日志，沿用已审查样式。

## 验证门槛与结果

- 真正的 Rust 服务、文件、原生 IPC 与进度；不用模拟后端代替验收。
- 模糊 Excel 原件匹配须人工确认；Manifest 映射验证原件哈希。
- 原始文件不覆盖，单包失败不损坏已归档数据，取消与批次撤销可恢复。
- 更新先排空操作、保留草稿并备份；真实公开升级验证全部归档元数据、来源、原件与设置。
- 三架构编译/安装检查、Tauri 更新签名、操作系统发行签名与公证分别记录，不相互替代。

## 当前剩余工作

1. 通过 GitHub Secrets 配置完整 Apple 发行/公证凭据后发布新 SemVer 版本；验证 codesign、stapler、spctl、真实 Mac 安装、交互式 WKWebView 与公开在线升级。
2. 使用专门测试百度凭据对合成发票执行明确授权后的真实 OCR 识别验收。
3. 如需 Windows 操作系统发行者认证，配置 Authenticode 证书；当前 Windows Tauri 签名升级已经可用。

详细结果与公开链接见 [acceptance.md](acceptance.md)。上述客观条件未满足时，Goal 不标记 complete。
