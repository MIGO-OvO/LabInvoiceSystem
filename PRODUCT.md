# LabInvoiceSystem

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

用户指定 Rust、Tauri 2、React、TypeScript、Vite。桌面 WebView 与独立静态门户共享品牌；原生窗口及系统对话框遵循 Windows/macOS 的惯例。

## Users

实验室成员整理自己的发票；报账人员接收多名成员提交的 ZIP，核对、分类、汇总并统一导出。

## Product Purpose

完成个人整理 → ZIP 导出 → 多人提交 → 批量接收 → 分类汇总 → 统一导出的可追溯本地工作流。

## Operating Context

中文财务工作，PDF/JPG/PNG 原件、旧版八字段 Excel、旁存 JSON，长期在普通桌面显示器或高 DPI 笔记本上核对。旧版有发票录入、导出归档、统计仪表盘三项侧栏导航及主题辅助操作。

## Capabilities and Constraints

办公/生活用品、实验用品为最高分类，缺失字段的历史数据保持未分类。录入日期和购买日期分别保存。云 OCR 必须明确授权，凭据由系统安全存储保存。原始文件本地保存；导入不能覆盖旧数据；导出携带版本化 Manifest 和 SHA-256。

## Brand Commitments

用户要求桌面保留旧版 Slate/Indigo 视觉风格和主要交互。门户专业、现代、克制，独立布局；不能使用虚假截图或占位下载地址。所有设计选择已由用户授权采用技能推荐方案，无需重复询问。

## Evidence on Hand

旧版 Models/Services/ViewModels/Views 已审计，见 docs/legacy-audit.md。许可证为 MIT。发布与截图是待验证事实，不得预先宣称；门户使用实际运行截图，截图中的数据只能是明确标识的合成验收样本。

## Product Principles

- 核对原件和字段是界面的首要任务。
- 本地事务与来源追踪使报账结果可复核。
- 保留旧版习惯，用反馈和排版降低操作负担。
- 失败可恢复，未知字段留给人工核对。

## Accessibility & Inclusion

中文可读，WCAG AA 对比度，键盘可操作，可见焦点，状态包含文字，减少动画设置，高 DPI 与移动门户适配。
