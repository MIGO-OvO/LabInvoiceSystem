# 原版业务审计与功能对照

## 固定审计基线

旧本地开发 HEAD `ddd81aec202f0a415ec22edae0fea1b9f5ac7e94`；旧远端 main `ff295217260f893771ea10479d048593fc61385a`。参考 D2 HEAD `8d5962b35c25d033acb07a37e6f4702b80f09fd6`。原工作区还有用户未提交产品文档与构建产物，均仅本地备份。

## 数据与真实业务

- InvoiceInfo：FileName、InvoiceDate（购买日期）、EntryDate（录入日期）、Amount(decimal)、ItemName、OcrItemName、PaymentMethod、InvoiceNumber、SellerName、SellerTaxId、FilePath、Status、RawOcrData、处理与校验状态。
- appsettings.json：旧系统 ApplicationData/LabInvoiceSystem 目录；归档/暂存/导出路径可能是相对程序工作目录。Windows SecretKeyEncrypted 为当前用户 DPAPI；macOS 旧版不持久化 Secret Key。迁移不能将密钥带入 ZIP 或源码。
- FileManagerService：按购买年月物理归档；旁存 PascalCase JSON 八字段；旧文件名 `YYYYMMDD-项目名称-支付方式-金额元.ext`。录入日期缺失时旧版取文件创建时间，新版只能作为注明来源的建议。
- InvoiceExportViewModel：展示按录入日期 → 购买日期分组，搜索跨所有业务字段；默认选中，支持全选、日期组/所选导出、单文件另存、编辑、删除确认。
- 八字段 Excel 顺序：发票录入日期、购买日期、金额、项目名称、支付方式、发票号码、销售方名称、销售方税号。旧 ZIP 在根目录存原件和 Excel，没有文件映射，且同名可能产生重复条目。
- OCR：百度 VAT endpoint；授权后上传；PDF 首页面渲染；名称/规格提取时有效规格优先，过滤纯数量/单位，项目缩写至十字符；按销售方税号或名称 + 原 OCR 项目记忆人工修正。
- 去重：同发票号结合销售方税号/名称；销售方缺失保守提示；另比较文件长度和 SHA-256；单张允许人工确认重复，批量遇冲突暂停。
- 统计：累积金额/数量、近30自然日（今天和此前29天）、购买日期每日热力图。设置在仪表盘配置 OCR；侧栏切换深浅主题，窄窗口折叠导航，预览滚轮缩放、拖动、适应窗口。

## 新旧功能矩阵（实现后更新验收状态）

| 成熟功能 | 3.0 对应实现 | 要求 |
| --- | --- | --- |
| PDF/JPG/JPEG/PNG 选择和拖入 | 原生多选/拖入，持久化草稿队列 | 保留原件 |
| 百度发票 OCR + 明确同意 | Rust 请求、系统凭据存储、核对面板 | 无自动上传 |
| 手工录入/校验/重试 | 九业务字段编辑，字段错误提示 | 金额>0、有效日期和名称 |
| PDF/图像预览 | 本地字节 PDF.js，高 DPI、分页、缩放与平移 | 无远程 PDF 服务 |
| 单张/全部归档 | 事务核对确认，重复冲突阻止误报 | 草稿跨重启恢复 |
| 统一购买日期/支付方式 | 批量修改草稿 | 保留两个日期语义 |
| 文件/身份去重 | 哈希与身份跨包/批次/历史核对 | 确认有审计理由 |
| 归档日期组/搜索/全选 | 分类 → 录入日期 → 购买日期 → 支付方式 | 原有路径习惯 |
| 编辑/删除/日期组批量操作 | 事务编辑、软删除及恢复、批量分组动作 | 可回滚 |
| ZIP + 八字段 Excel | 增加第九分类和原件映射，Manifest v1 | 旧八字段保持顺序 |
| 单文件另存/导出目录 | 原生另存与打开路径 | 不无提示覆盖 |
| 仪表盘/热力图/API用量 | 实际归档统计、分类独立统计及用量 | 精确金额 |
| 主题/本地设置/日志 | 深浅系统主题、持久化设置/操作记录 | 日志不记录凭据 |
| 历史旁存 JSON 和设置 | 只读预览、备份后复制导入、可撤销批次 | 不修改旧目录 |
| 新增：多 ZIP 接收 | 独立安全解析、Excel 人工匹配、真实进度 | ZIP Slip/炸弹防护 |
| 新增：安全更新 | 官方插件、签名校验、延迟/定期/手动 | 写入排空与快照 |

## D2 发布审计

[前端更新](https://github.com/MIGO-OvO/D2-Morgeth-Kick/blob/8d5962b35c25d033acb07a37e6f4702b80f09fd6/src/App.tsx) 延迟4.5秒、每12小时、手动检查、跳过版本与真实下载事件；[API](https://github.com/MIGO-OvO/D2-Morgeth-Kick/blob/8d5962b35c25d033acb07a37e6f4702b80f09fd6/src/api.ts) 在安装前 prepare_for_update，异常时解锁，成功重启。D2 的 dev mock 不移入本产品。

[Rust](https://github.com/MIGO-OvO/D2-Morgeth-Kick/blob/8d5962b35c25d033acb07a37e6f4702b80f09fd6/src-tauri/src/lib.rs) updater 插件和操作锁适合参考；Win32 输入、浮窗、全局按键专属代码不适用。

[Release](https://github.com/MIGO-OvO/D2-Morgeth-Kick/blob/8d5962b35c25d033acb07a37e6f4702b80f09fd6/.github/workflows/release.yml) Windows NSIS + tauri-action、Secrets 签名、draft Release、latest.json 和 .sig；3.0 扩为三个架构并显式校验全部平台清单，Apple 正式分发增加签名公证门槛。[Pages](https://github.com/MIGO-OvO/D2-Morgeth-Kick/blob/8d5962b35c25d033acb07a37e6f4702b80f09fd6/.github/workflows/pages.yml) 采用官方 Pages artifact/deploy 权限模式，门户下载链接只指向实际存在的资产。
