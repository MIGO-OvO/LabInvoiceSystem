# LabInvoiceSystem 2.0.0

## 主要更新

- 发票归档按“录入日期 → 购买日期”两级分组，并支持手动调整归档日期组。
- 重新整理录入、归档导出和统计页面，改善小窗口布局、操作密度与滚动体验。
- 增强批量录入、重复发票识别、OCR 隐私确认、人工录入和归档元数据恢复。
- 提供 Windows、Linux、macOS 的 x64 与 arm64 自包含发布包。

## 平台说明

- Windows：删除归档文件时使用系统回收站；OCR Secret Key 使用当前用户 DPAPI 加密保存。
- Linux/macOS：删除前会明确提示永久删除；Secret Key 默认仅保留在当前会话，也可通过 `LABINVOICESYSTEM_BAIDU_SECRET_KEY` 环境变量提供。
- macOS 发布包未签名，首次运行可能需要在“隐私与安全性”中确认打开。
