# 3.0 实施计划与阶段状态

## 授权与工作边界

用户已授权独立重构、备份、重命名旧仓库、新建仓库、提交推送、合并、Pages 与 Release。旧工作区的未提交 PRODUCT.md、已跟踪构建产物及其变更保持原样。新代码在独立 LabInvoiceSystem-v3 目录与 codex/v3-rebuild 分支开发。备份在相邻 .labinvoice-backups/20261013-v3，绝不提交。

## 分阶段执行

1. **审计（已完成代码审计，备份下载进行中）**：记录旧业务、实际字段、归档关系、OCR 规则，保存完整本地 Bundle、远端 mirror、原工作区 binary diff、未提交产品文档、仓库与 Release 元数据及全部 Release 产物。验证后重命名并核对仓库 ID、全部 refs、Release ID 与资产。
2. **规划（当前）**：独立 Rust core + Tauri commands；SQLite 事务存元数据，SHA-256 地址保存不可变原件；导入草稿与任务持久化；归档前核对；软删除可恢复；Manifest v1 精确映射；整数分处理金额；本地凭据 Windows Credential Manager/macOS Keychain。
3. **实施**：先实现并测试 core（多 ZIP、安全、Excel、迁移、去重、统计），再连接原生命令、OCR、PDF 预览和完整 React 工作流。实现 updater 安全锁、跨平台 CI、Release、门户。
4. **验证**：执行 Rust 测试、Clippy、前端类型检查与测试、真实 Windows 构建及业务 E2E；GitHub Actions 验证 Windows x64、macOS Intel/ARM 构建。验收恶意 ZIP、错误元数据、取消、重试、跨批次重复、历史迁移、无损回导和失败恢复。
5. **发布**：验证通过后合并 main，SemVer v3.0.0，Pages 正式部署；签名私钥只进仓库 Secrets。Apple 证书或公证凭据缺失时如实记录正式分发阻塞，不将未公证产物宣称为正式可安装产品。
6. **交付**：新旧功能矩阵、安装/更新证据、CI 与 Pages 地址、已发布资产链接、剩余阻塞及干净的新工作区。

## Impeccable 执行

已读取已安装的 4.0.4 SKILL.md，并在新项目运行 context.mjs --target portal/index.html。init 产品事实来自完整用户 brief 与旧版代码；用户特别授权覆盖技能的人工采访和选择等待环节。shape 为桌面保留既有导航/核对结构，门户呈现产品真实工作流。后续记录概念种子、布局/排版决策、critique/polish/audit 及独立 finish reviewer 和 documenter 结果。不得将设计图当作产品截图。

## 验证门槛

- 不接受模拟后端、未连接命令或随机进度。
- 不自动选择模糊 Excel 对应原件；未匹配行可人工配对。
- 原始文件无覆盖，导入单包失败不损坏已归档数据。
- 更新必须排空正在写入的事务，保留草稿，成功备份后才安装。
- 正式签名/公证与真实安装验证是独立状态，编译通过不能替代。
