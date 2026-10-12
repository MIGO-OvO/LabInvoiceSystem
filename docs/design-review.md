# Impeccable 实施与审查

实际使用已安装的 Impeccable 4.0.4。用户明确委托所有设计选择，按推荐方案自主执行。

## 流程与依据

- init：PRODUCT.md 记录实验室个人整理与报账人员接收任务，桌面保留旧版导航和 Slate/Indigo 品牌。
- shape/craft：独立门户采用品牌扩展。概念种子 `7f532313`；A/B/C 三个真实设计构图已生成和查看，A 的批准记录保存在 sidecar 与 surface brief。
- layout/typeset：先确认左右产品证明结构与移动重排，再核实 Noto Sans SC 变量字体；修复静态字体误取导致的字重失效、流程文字拼接、平板流程拥挤及移动导航焦点顺序。两项专项 detector 均无发现。
- critique：独立 A `/root/portal_critique_a` 与 B `/root/portal_critique_b`，无相互共享结论。A 检查图与实际页面；B 运行一次官方 detector（`[]`，exit 0）及新建浏览器页面的只读测量、键盘、日志和截图。
- polish：按独立 finish reviewer 的五项材料修复统一处理：桌面标题轮廓、Apple 平台 SVG、箭头 SVG、安装说明深链展开与焦点、按真实资产改变发布文案。
- audit：Playwright/axe 在 320、390、768、1440px、深浅主题和 200% 文字下检查，无 WCAG AA 违规或横向溢出；真实截图均由 Tauri 窗口产生，数据明确标记为合成验收。
- finish/document：独立 `/root/portal_finish` 最终 `disposition: ship`；五项修复全部 resolved，remaining clear。独立 documenter 从真实代码写出根 DESIGN.md 与 `.impeccable/design.json`，验证 token、组件、节次、JSON 与 narrative 一致。

## 独立 critique 评分（发布前状态）

Method: dual-agent（A: `/root/portal_critique_a` · B: `/root/portal_critique_b`）。

| Nielsen 启发式 | 分数 | 当时主要观察 |
| --- | --- | --- |
| 状态可见性 | 2 | 实际未发布时下载动作禁用，Releases 回退可用 |
| 现实世界匹配 | 3 | 核对、接收与分类符合业务，技术说明较密 |
| 控制与自由 | 4 | 主题、浏览和 disclosure 可逆 |
| 一致性 | 3 | 预览状态文案需统一（已修复） |
| 错误预防 | 3 | 不使用虚假下载 URL，平台风险明确 |
| 识别优于记忆 | 3 | 架构与角色步骤清楚 |
| 灵活性与效率 | n/a | Persuade 门户无专家批量操作任务 |
| 美观与精简 | 3 | 中文层级与空间清楚，协议说明有阅读成本 |
| 错误恢复 | 3 | 下载元数据失败时仍有实际 Releases 链接 |
| 帮助与文档 | 3 | 两类使用者说明；深链需直接展开（已修复） |
| 合计 | 27/36 | 75%，Good；未伪造发布后的重新评分 |

认知负荷为中等：当时的禁用下载占首屏、协议描述密集。五步流程是有意义的顺序，三个架构是平台选择。发布后下载从已存在的资产生成；没有为改善评分伪造可用状态。

## 技术 audit

| 维度 | 分数 | 证据与边界 |
| --- | --- | --- |
| 无障碍 | 3/4 | axe AA、焦点、skip link、44px主要操作通过；未做真实屏幕阅读器人工验收 |
| 性能 | 3/4 | 门户无框架运行时，字体自部署，原图按需加载；未做低端设备长时间性能测量 |
| 主题 | 4/4 | 实际深浅截图与对比度通过，主题持久化 |
| 响应式 | 4/4 | 窄屏、手机、平板、桌面与文字放大通过 |
| 实现完整性 | 4/4 | 官方 detector 无发现；真实软件截图、实际状态、独立审查完成 |
| 合计 | 18/20 | 自动与人工视觉证据范围见上表 |

## Run Notes

目标 slug：`portal-index-html`。无 ignore list，A/B 保持独立。B 的浏览器始终 hidden；API 明示 evaluate 只读、无 DOM mutation，因此未注入 overlay、未启动 live-server；采用一次 CLI、原生截图、只读 DOM、键盘和日志作为明确 fallback。独立临时浏览器页已关闭、视口/主题状态已恢复，无临时源码修改。技术自动化截图与报告留在 ignored `test-results/`，正式产品图保存在 `portal/assets/`。

发布/签名事实继续以 Release inventory 与 acceptance.md 为准；设计结论不替代软件安全、安装或更新验收。
