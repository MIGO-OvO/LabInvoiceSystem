---
name: "LabInvoiceSystem"
description: "可核对的本地发票工作台与同品牌发布门户"
colors:
  indigo: "#4f46e5"
  indigo-hover: "#4338ca"
  indigo-light: "#a5b4fc"
  indigo-tint: "#e0e7ff"
  desktop-hover-dark: "#2b3553"
  desktop-hover-light: "#eef2ff"
  portal-tint-dark: "#293354"
  canvas-light: "#f8fafc"
  paper-light: "#fff"
  field-light: "#f1f5f9"
  ink-light: "#0f172a"
  secondary-light: "#475569"
  line-light: "#cbd5e1"
  canvas-dark: "#0f172a"
  paper-dark: "#182337"
  field-dark: "#1e293b"
  ink-dark: "#f1f5f9"
  desktop-muted-dark: "#a8b7cc"
  desktop-line-dark: "#35465f"
  desktop-input-dark: "#111b2d"
  portal-secondary-dark: "#b6c2d4"
  portal-line-dark: "#41516a"
  success-dark: "#6ee7b7"
  success-light: "#047857"
  error-dark: "#fca5a5"
  error-light: "#b91c1c"
  danger-dark: "#442331"
  danger-light: "#fef2f2"
  warning-dark: "#fcd34d"
  warning-light: "#854d0e"
typography:
  portal-display:
    fontFamily: "Portal Sans, \"Noto Sans SC\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "clamp(1.8rem, 3.25vw, 3.1rem)"
    fontWeight: 750
    lineHeight: 1.24
    letterSpacing: "-0.025em"
  portal-headline:
    fontFamily: "Portal Sans, \"Noto Sans SC\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "clamp(1.85rem, 2.8vw, 2.8rem)"
    fontWeight: 750
    lineHeight: 1.24
    letterSpacing: "-0.025em"
  portal-title:
    fontFamily: "Portal Sans, \"Noto Sans SC\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "1.25rem"
    fontWeight: 650
    lineHeight: 1.5
  portal-body:
    fontFamily: "Portal Sans, \"Noto Sans SC\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "1rem"
    lineHeight: 1.85
  desktop-headline:
    fontFamily: "\"Segoe UI Variable\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "24px"
    fontWeight: 650
    lineHeight: 1.4
    letterSpacing: "-0.025em"
  desktop-title:
    fontFamily: "\"Segoe UI Variable\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "17px"
    fontWeight: 650
    lineHeight: 1.5
  desktop-body:
    fontFamily: "\"Segoe UI Variable\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "14px"
    lineHeight: 1.65
  desktop-label:
    fontFamily: "\"Segoe UI Variable\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "12px"
    fontWeight: 550
rounded:
  desktop-badge: "5px"
  desktop-field: "6px"
  desktop-button: "7px"
  portal-action: "8px"
  desktop-container: "10px"
  surface: "12px"
spacing:
  inline: "8px"
  tight: "12px"
  desktop-group: "16px"
  group: "24px"
  portal-section: "96px"
  portal-section-compact: "64px"
components:
  desktop-button-primary:
    backgroundColor: "{colors.indigo}"
    textColor: "{colors.paper-light}"
    rounded: "{rounded.desktop-button}"
    padding: "7px 12px"
  desktop-button-primary-hover:
    backgroundColor: "{colors.indigo-hover}"
  desktop-button-secondary-dark:
    backgroundColor: "{colors.paper-dark}"
    textColor: "{colors.ink-dark}"
    rounded: "{rounded.desktop-button}"
    padding: "7px 12px"
  desktop-input-dark:
    backgroundColor: "{colors.desktop-input-dark}"
    textColor: "{colors.ink-dark}"
    rounded: "{rounded.desktop-field}"
    padding: "7px 9px"
  desktop-nav-active-dark:
    backgroundColor: "{colors.desktop-hover-dark}"
    textColor: "{colors.indigo-light}"
    rounded: "{rounded.desktop-button}"
    padding: "10px 12px"
  desktop-badge-dark:
    backgroundColor: "{colors.desktop-hover-dark}"
    textColor: "{colors.indigo-light}"
    rounded: "{rounded.desktop-badge}"
    padding: "4px 7px"
  desktop-metric-dark:
    backgroundColor: "{colors.paper-dark}"
    rounded: "{rounded.desktop-container}"
    padding: "16px 20px"
  desktop-review-dark:
    backgroundColor: "{colors.paper-dark}"
    rounded: "{rounded.surface}"
  portal-download-primary:
    backgroundColor: "{colors.indigo}"
    textColor: "{colors.paper-light}"
    rounded: "{rounded.portal-action}"
    padding: "12px 15px"
  portal-download-secondary:
    backgroundColor: "{colors.paper-light}"
    textColor: "{colors.ink-light}"
    rounded: "{rounded.portal-action}"
    padding: "12px 15px"
  portal-download-disabled:
    backgroundColor: "{colors.paper-light}"
    textColor: "{colors.secondary-light}"
  portal-help:
    textColor: "{colors.ink-light}"
---

# Design System: LabInvoiceSystem

## Overview

**Creative North Star: "可核对的本地工作台"**

以可核对的本地工作台为视觉中心：冷灰 Slate 承载原件、字段、表格和来源，Indigo 标记操作与选中状态。桌面延续已有导航习惯和紧凑的中文财务信息密度，默认深色；门户是同品牌的明亮办公室扩展，默认浅色，提供更宽的段落节奏和实际运行画面。

这份系统来自 src/styles.css、portal/styles.css，以及 App、InvoiceEditor、Dashboard 和门户的已构建组件。两种表面共享色彩家族与清晰的轮廓，但字体、字号、边界色和断点按各自用途保留。门户的分栏首屏与五步交接顺序留在 surface brief，不定义所有新页面的构图。

**Key Characteristics:**

- 冷灰底面与 Indigo 操作色，深浅主题均保留文字状态。
- 桌面系统中文字体与紧凑表格；门户自主部署 Noto Sans SC。
- 实体轮廓与柔和圆角；阴影用于原件、真实截图和模态层。
- 可见键盘焦点、减少动画、窄屏重排和可滚动数据。

## Colors

冷灰色面提供稳定的阅读背景，Indigo 集中承载动作与选择；前置 token 是实际 CSS 值，后文描述其用途。

### Primary

- **工作 Indigo**：indigo 用于两个表面的实心主动作；indigo-hover 对应可用主动作的悬停。
- **浅 Indigo**：indigo-light 是深色主题的链接、焦点和选中前景。热力图保留自身四级 Indigo 数值，不把数据可视化色阶当作链接交互色。
- **Indigo 衬底**：桌面选中/悬停使用 desktop-hover-dark 或 desktop-hover-light；门户链接按钮悬停使用 indigo-tint 或 portal-tint-dark。

### Secondary

- **确认绿**：success-dark / success-light 表达本地就绪、分类标记与 API 状态，始终配有文字。
- **错误红**：error-dark / error-light 与 danger-dark / danger-light 承载校验与失败；warning-dark / warning-light 是待处理冲突的提示。

### Neutral

- **画布与纸面**：canvas、paper、field 的 light/dark 组分别对应外层、内容面和内嵌预览。相同值的不同角色仍保留语义。
- **墨色与辅助色**：ink 组用于主文字，secondary-light 为共享浅色辅助文字；桌面深色使用 desktop-muted-dark，门户深色使用 portal-secondary-dark。
- **边界与输入**：浅色共享 line-light；桌面深色使用 desktop-line-dark、desktop-input-dark，门户深色使用 portal-line-dark。门户输入面角色是 field，不继承桌面输入专用色。

**The Theme Roles Rule.** 切换主题时按表面映射背景、文字、边界和状态色；实心主按钮保持 Indigo 填充与白字。桌面与门户的相近角色不强行合并为同一个源变量。

## Typography

**Display Font:** 门户自主部署的 Portal Sans（Noto Sans SC 变量字体），其回退顺序见 portal 字体 tokens。
**Body Font:** 桌面 Segoe UI Variable / Segoe UI 与中文系统回退；门户沿用其标题字体栈。
**Label/Mono Font:** 桌面标签使用同一系统字体；门户技术文件名采用 ui-monospace, monospace。

中文可读与数值比较决定排版。门户标题厚重且紧凑，段落舒展；桌面标题与字段缩短扫描距离。没有全项目统一的等比字号尺度。

### Hierarchy

- **Display**：portal-display 用于门户主要标题；在 800px 以下切换为 clamp(2.5rem, 7vw, 3.3rem)，520px 以下为 2.5rem。
- **Headline**：portal-headline 用于门户章节；desktop-headline 用于桌面页标题，900px 以下缩为 22px。
- **Title**：portal-title 用于基础三级标题；desktop-title 用于桌面区域标题。门户功能标题单独强调，不升级为全局 title token。
- **Body**：portal-body 段落最大约 70ch；desktop-body 是桌面根字号，桌面段落行高 1.65。表格和状态使用更小的上下文字号。
- **Label**：desktop-label 用于字段与过滤器；次要元信息常为 11–12px。金额、身份和日期序号使用 tabular-nums。

**The Read Before Ornament Rule.** 金额和身份字段使用等宽数字特性；辅助说明降为次级文字，但核对字段、状态和动作仍用文字表达。

## Layout

桌面基于固定侧栏与弹性主区：侧栏 200px，主区左右 24px；1100px 以下为 178px / 18px。900px 以下导航转为顶端横排，主区左右 16px。主工作区可用宽度不超过 900px 时原件核对和汇总区改为单列，620px 以下指标也改为单列。核对面板自身不超过 420px 时字段转为单列。核对主区在宽屏是 minmax(270px, 2fr) 与 minmax(0, 3fr)，间隔 16px；字段默认双列、间隔 12px。40px 自定义标题栏固定在顶部；主工作区单独滚动，34px 状态栏保留底部空间。最小窗口 640 × 480，短窗口侧栏和长列表仍可滚动。所有滚动条隐藏，滚轮、触控板及键盘滚动保留；表格与热力图在受限宽度保留横向滚动。

门户内容最大 1360px，初始左右总留白 96px；1100px、800px、520px 以下分别为 64px、40px、32px。800px 以下主要内容从双列转单列，导航换行；五步流程改成纵向条目，520px 以下下载动作纵排。章节节奏从 96px 缩为 64px。

共享日常间距是 8px、12px、16px、24px；具体组件保留自己的 padding。门户宽屏引导构图与截图顺序由 .impeccable/surfaces/portal-index-html.md 管理。

## Elevation & Depth

普通桌面容器使用纸面底色与 1px 轮廓，门户阅读区依靠灰底交替和分隔线。柔和阴影只在实际图像承载层与模态层形成重点深度。

### Shadow Vocabulary

- **原件纸张**（0 5px 16px #080f1926）：桌面预览里的 canvas 或图像。
- **门户证据图**（0 18px 48px #0f172a24）：可打开的真实产品截图。
- **模态层**（0 20px 70px #030b194d）：居中确认对话框；遮罩使用 #030b1baa。

**The Evidence Depth Rule.** 常规信息容器靠底色与边界分层；柔和投影承载原件、实际截图和对话框。

门户证据图以 650ms cubic-bezier(.16,1,.3,1) 进入；下载背景过渡为 180ms ease-out，桌面按钮反馈为 120ms、页面与开关 180ms、对话框进入 220ms / 退出 160ms，统一使用 cubic-bezier(.16,1,.3,1)。页面位移 4px，动效仅表达状态和连续性；减少动画设置关闭这些变化，工作进度转动只表达正在处理。

## Shapes

圆角由用途分级：桌面标签 5px、输入 6px、按钮 7px；门户动作 8px；桌面列表和指标容器 10px；核对面板、分组、对话框及门户截图 12px。圆形只用于步骤数字和状态点。输入与常规容器使用实体细边界；拖入区和不可用下载以虚线明确特殊状态。

## Components

### Buttons

桌面动作为紧凑、文字主导的工具按钮。主按钮 Indigo 填充与白字，次按钮纸面与边界；基础最小高度 36px、padding 7px 12px。悬停次动作改变底色与边界，主动作变为 indigo-hover；禁用为 opacity .55 与不可操作光标。桌面焦点为 3px 主色轮廓、偏移 3px。

门户下载是带平台文字和辅助说明的链接动作，基础最小高度 58px、padding 12px 15px、圆角 8px。主动作白字/Indigo，次动作纸面/墨色。无真实下载地址时 aria-disabled=true、无 href、次级文字与虚线；可用状态才启用悬停。520px 以下高度至少 60px，水平 padding 20px。门户焦点轮廓偏移 5px。

### Chips

桌面核对状态为 5px 圆角、padding 4px 7px、11px 字号；Indigo 前景与选中底色。待处理冲突改为 warning 色，标签仍写出状态。

### Cards / Containers

核对面板使用 12px 圆角、实体轮廓与裁切；标题区 padding 14px 16px，表单 16px。列表记录和指标使用 10px 圆角。指标内部 padding 16px 20px，标签 12px，数字 26px / 600、主色与 tabular-nums；随桌面断点缩为 23px 和 21px。

### Inputs / Fields

桌面输入和选择器使用专用输入底面、6px 圆角、1px 边界、最小高度 36px、padding 7px 9px。字段标签 12px / 550，值 14px / 400；aria-invalid=true 使用错误边界。焦点沿用桌面通用轮廓，禁用由 fieldset 与输入的原生语义管理。

### Navigation

桌面三个主要入口保留已有工作流程顺序，宽屏侧栏按钮最小高度 44px、padding 10px 12px、字重 550；active 使用选中底色和主色。900px 以下转顶部紧凑横排。门户导航是次级色文字链接，悬停变为 accent，最小高度 44px；800px 以下换到独立一行。主题切换是 44px 方形、8px 圆角描边按钮，具有动作名称。

### Disclosure and Provenance

门户帮助使用原生 details/summary，实体上下分隔线、summary 最小高度 44px、600 字重，内容留出阅读宽度。桌面来源区以顶部分隔线、12px 辅助文字、可换行来源名称和哈希提供追踪信息；展开行为保留原生键盘语义。

## Do's and Don'ts

### Do:

- Do 根据目标表面选择桌面或门户的真实字体、密度与主题变量。
- Do 将原件、字段、分类和来源保持在可核对的信息层级中。
- Do 保留可见焦点、文字状态、减少动画设置与窄屏重排。
- Do 使用真实运行截图，并对合成验收数据明确标注。

### Don't:

- Don't 让门户的大标题、段落间距或首屏构图替代桌面的核对密度。
- Don't 用颜色单独表示核对、冲突、错误或下载可用性。
- Don't 将不可用的安装包表现为可下载的主动作。
- Don't 将单个页面的布局或一次性的视觉数值推广为全项目规则。
