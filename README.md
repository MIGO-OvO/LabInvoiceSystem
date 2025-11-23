# Lab Invoice System

> LabInvoiceSystem is a desktop tool for managing reimbursement invoices in laboratories, with OCR-based importing, archiving, exporting and statistics.
GitHub: https://github.com/MIGO-OvO/LabInvoiceSystem

LabInvoiceSystem 是一个面向个人实验室课题组发票报账场景的发票管理桌面应用，基于 Avalonia 构建，提供发票录入（OCR 识别）、归档导出和统计分析等功能，帮助简化报销资料整理流程，适合在实验室和科研场景中自由使用与二次开发。

---

## 目录

- [项目简介](#项目简介)
- [功能特性](#功能特性)
- [快速开始](#快速开始)
- [使用指南](#使用指南)
  - [发票录入](#发票录入)
  - [发票归档与导出](#发票归档与导出)
  - [统计分析](#统计分析)
  - [OCR API 设置与测试](#ocr-api-设置与测试)
- [配置说明](#配置说明)
- [目录结构与架构概览](#目录结构与架构概览)
- [技术栈与依赖](#技术栈与依赖)
- [常见问题 FAQ](#常见问题-faq)
- [License 与致谢](#license-与致谢)

---

## 项目简介

在实验室或科研单位的日常报销流程中，研究人员往往需要管理大量零散的纸质或电子发票，并按照时间、项目、支付方式等维度进行整理与统计。手工处理不仅耗时，还容易出错。

LabInvoiceSystem 旨在解决这些问题，提供：

- **发票录入**：支持 PDF / JPG / PNG 文件，集成百度增值税发票 OCR 自动识别关键字段。 
- **发票归档与导出**：使用统一文件命名规则（日期 + 项目名称 + 支付方式 + 金额）进行归档，支持按日期导出 ZIP。 
- **统计分析**：提供累计报销金额、发票数量、近 30 天报销金额、年度热力图等数据视图。

应用采用 MVVM 架构，基于 Avalonia 跨平台 UI 框架开发，目前主要以 Windows 桌面环境为运行目标。

---

## 功能特性

- **发票录入与预览**  
  - 支持拖拽或通过文件选择对话框上传多个发票文件。  
  - 支持 PDF / JPG / JPEG / PNG。PDF 会通过内部的 PDF 转图片服务转换为首张图片进行预览和 OCR。  
  - 所有上传文件首先保存到临时目录 `temp_uploads`。  
  - 右侧提供发票预览区域，方便快速核对内容。

- **OCR 自动识别**  
  - 集成百度增值税发票 OCR（VAT Invoice OCR）接口，由 `OcrService` 负责调用与结果解析。  
  - 自动尝试识别：开票日期、价税合计金额、项目名称 / 货物名称等字段。  
  - 识别结果填充到 `InvoiceInfo` 模型中，用户可以在界面中进一步修改。  
  - OCR 失败时，会将错误信息写入发票的 `RawOcrData` 字段，并提示用户手动编辑。

- **发票归档与文件管理**  
  - 单张归档：在录入界面中选择发票后点击“确认并归档”，由 `FileManagerService` 将文件从临时目录移动到归档目录。  
  - 批量归档：支持“一键全部归档”，会对所有信息完整的发票进行批量处理。  
  - 归档命名规则： `YYYYMMDD-项目名称-支付方式-金额元.ext`，并按月份分目录存放：`archive_data/YYYY-MM/`。  
  - 支持删除临时文件和删除归档文件。  
  - 支持将某一日期下的全部归档发票打包为 ZIP 文件导出，同时自动生成形如 `YYYYMMDD_报账发票明细.xlsx` 的 Excel 明细并一并打包。

- **统计分析**  
  - 基于归档文件的解析结果（`ArchiveItem` + `StatisticsService`），计算：  
    - 累计报销金额。  
    - 累计发票数量。  
    - 近 30 天报销金额。  
  - 生成过去一年的报销热力图（按日聚合金额，并以不同颜色深浅表示强度）。  
  - 展示 OCR API 配置状态（例如“API 就绪”或“未配置”），便于快速确认 Baidu OCR 凭据是否已正确填写。  
  - 展示本月 OCR 调用次数与配额（例如“本月调用: X/Y”），其中 Y 来自 `AppSettings.BaiduMonthlyQuota`。

- **主题与用户体验**  
  - 使用 Fluent 风格的现代 UI，搭配卡片式布局与渐变色。  
  - 支持浅色 / 深色主题切换（ThemeVariant），当前选择会同步写入配置 `AppSettings.ThemeMode`。  
  - 侧边导航与动画切换增强整体操作体验。

- **日志记录**  
  - 通过 `LoggerService` 将用户的关键操作（上传、归档、删除、导出）写入 JSON 日志文件：  
    - 路径：`%APPDATA%/LabInvoiceSystem/upload_logs.json`。  
  - 便于后续追踪使用记录或排查问题。

---

## 快速开始

### 环境要求

- 推荐操作系统：**Windows 10 或更高版本**。  
- 对于开发者：建议安装 **.NET 8 SDK** 以便从源码构建与调试。  
- 对于仅运行已发布可执行文件的普通用户：至少需要安装 **.NET 8 Desktop Runtime**（框架依赖部署，未安装运行时将无法启动程序）。  
- 应用基于 Avalonia 理论上具备跨平台能力，但仓库中的启动脚本 `start.bat` 以及下文的发布示例主要面向 Windows 环境。

### 获取代码

```bash
# 克隆仓库
git clone https://github.com/MIGO-OvO/LabInvoiceSystem.git
cd LabInvoiceSystem
```

### 运行应用

#### 方式一：使用已发布的可执行文件（win-x64）

1. 确认已安装 **.NET 8 Desktop Runtime**。  
2. 从Release下载对应电脑架构版本的压缩包后，解压压缩包到独立文件夹内。
3. 在该目录下找到可执行文件（`LabInvoiceSystem.exe`），直接双击即可启动应用。  
4. 如启动时系统提示缺少 .NET 运行时，请先从 Microsoft 官方网站安装对应版本的 **.NET 8 Desktop Runtime** 后重试。

#### 方式二：命令行运行

```bash
cd LabInvoiceSystem

dotnet run
```

#### 方式三：Windows 批处理脚本

在仓库根目录下执行：

```bat
start.bat
```

脚本会自动进入 `LabInvoiceSystem` 子目录并执行 `dotnet run`，同时在启动失败时保留命令行窗口以便查看错误信息。  

### OCR 凭据配置概览

项目内的 `AppSettings` 类型定义了百度 OCR 所需的凭据字段：

- `BaiduAppId`
- `BaiduApiKey`
- `BaiduSecretKey`

应用实际运行时，会通过 `SettingsService` 从配置文件加载这些值：

- 配置文件路径：`%APPDATA%/LabInvoiceSystem/appsettings.json`。  
- 如果文件不存在，会使用默认配置并在保存时自动创建。  

---

## 使用指南

### 发票录入

1. 打开应用后，左侧导航选择 **“发票录入”**。  
2. 在左侧区域通过以下任一方式上传发票：  
   - 点击“上传文件”按钮，打开系统文件选择对话框；  
   - 直接将 PDF / 图片文件拖拽到左侧“拖拽发票到此处”区域。  
3. 已上传的发票会显示在左侧列表中，并在后台自动完成：  
   - 保存文件至临时目录 `temp_uploads`；  
   - 对 PDF 文件执行 PDF→图片转换（首张）；  
   - 调用百度 OCR 接口进行识别。
4. 点击某一条发票记录，右侧会显示：  
   - 发票图片预览；  
   - 下方编辑表单：日期、金额、项目名称、支付方式等。  
5. 如需统一设置日期，可使用右上方的日期选择器与“一键设日期”功能，对当前批次所有发票应用同一日期。

### 发票归档与导出

#### 在录入界面归档

1. 在右侧编辑表单中确认以下字段已正确填写：  
   - 日期  
   - 金额  
   - 项目名称  
   - 支付方式（如“公务卡”、“现金”等）
2. 点击 **“确认并归档”**：  
   - 应用会验证金额与项目名称是否填写。  
   - 通过 `FileManagerService` 将对应文件从 `temp_uploads` 移动到归档目录：  
     - 目录格式：`archive_data/YYYY-MM/`。  
     - 文件命名：`YYYYMMDD-项目名称-支付方式-金额元.ext`。
3. 如需一次性归档所有信息完整的发票，可使用 **“全部归档”** 按钮。

#### 在“发票导出”界面管理归档数据

1. 左侧导航选择 **“发票导出”**。  
2. 点击“刷新列表”按钮，系统会：  
   - 从归档目录 `archive_data` 读取所有归档文件；  
   - 解析文件名并生成 `ArchiveItem` 列表；  
   - 按日期（`YYYY-MM-DD`）分组展示。  
3. 在某天的分组卡片右上角可以：  
   - **“导出为 ZIP”**：将该日期下所有发票打包为 ZIP，保存到你指定的位置。  
   - **“删除”**：删除该日期下所有归档文件。  
4. 在分组表格中每一条发票记录行的“操作”列可以：  
   - 单独 **下载** 该发票文件到磁盘任意位置；  
   - 单独 **删除** 该发票文件。

### 统计分析

1. 左侧导航选择 **“统计面板”**。  
2. 点击“刷新数据”按钮，系统会：  
   - 再次扫描归档目录 `archive_data`；  
   - 计算：累计报销金额、发票总数、近 30 天报销金额；  
   - 聚合每日支出数据，并生成过去一年的热力图。  
3. 在统计界面中你可以看到：  
   - 顶部三个 KPI 卡片展示关键数字。  
   - 中间为按日划分的热力图，鼠标悬停可查看某日的具体金额及日期。  
   - 右下角的图例展示从“少”到“多”的颜色梯度含义。  
   - 顶部状态区域中的 OCR API 配置状态（如“API 就绪”或“未配置”），以及本月调用次数与配额（例如“本月调用: X/Y”）。

### OCR API 设置与测试

1. 在统计面板中点击 **“API 设置”**（或等效入口），打开 OCR 配置对话框。  
2. 在弹窗中填写：  
   - `API Key`（对应 `AppSettings.BaiduApiKey`）  
   - `Secret Key`（对应 `AppSettings.BaiduSecretKey`）  
3. 点击 **“测试连接”**：  
   - 程序会调用百度 OAuth 接口校验凭据是否有效；  
   - 成功时提示“连接成功，API Key 有效”，失败时会显示错误描述。  
4. 点击 **“保存”**：  
   - 程序会将凭据写入 `%APPDATA%/LabInvoiceSystem/appsettings.json`；  
   - 同时刷新统计面板中的 API 状态和“本月调用: X/Y” 显示。  

---

## 配置说明

### AppSettings（`Models/AppSettings.cs`）

`AppSettings` 类型定义了应用运行时的重要配置，主要包括：  

- **OCR 配置**：  
  - `BaiduAppId`：百度 OCR 应用 ID。  
  - `BaiduApiKey`：百度 OCR API Key。  
  - `BaiduSecretKey`：百度 OCR Secret Key。  
  - `BaiduMonthlyUsage`：当前统计月份内已调用次数，由应用在每次识别后自动递增。  
  - `BaiduMonthlyQuota`：当前月份允许的最大调用次数，用于在界面中展示“本月调用: X/Y”。  
  - `BaiduUsageMonth`：当前统计的月份（例如 `"2025-11"`），用于在跨月时自动重置调用计数。  
- **目录配置**：  
  - `ArchiveDirectory`：发票归档根目录，默认值为 `"archive_data"`。  
  - `TempUploadDirectory`：临时上传目录，默认值为 `"temp_uploads"`。  
  - `ExportDirectory`：发票导出（ZIP + Excel）的默认目录，默认值为 `"export_data"`。  
- **主题配置**：  
  - `ThemeMode`：主题模式，`"Dark"` 或 `"Light"`。

### SettingsService（`Services/SettingsService.cs`）

- 采用单例模式 `SettingsService.Instance` 管理配置。  
- 配置文件路径：  
  - Windows 示例：`%APPDATA%/LabInvoiceSystem/appsettings.json`。  
- 启动时逻辑：  
  - 若配置文件存在：读取 JSON 并反序列化为 `AppSettings`。  
  - 若不存在：使用默认 `AppSettings` 实例。  
- 在保存配置时会确保：  
  - 配置目录存在；  
  - 结合 `EnsureDirectoriesExist` 方法，保证 `ArchiveDirectory`、`TempUploadDirectory` 以及非空的 `ExportDirectory` 对应的目录存在，不存在会自动创建。

---

## 目录结构与架构概览

### 关键目录结构（简要）

```text
LabInvoiceSystem/
├─ LabInvoiceSystem/           # 主项目
│  ├─ App.axaml                # 应用入口 XAML
│  ├─ App.axaml.cs             # 应用入口代码，初始化 MainWindowViewModel
│  ├─ Program.cs               # .NET 程序入口
│  ├─ Views/                   # 视图（XAML）
│  │  ├─ MainWindow.axaml
│  │  ├─ InvoiceImportView.axaml
│  │  ├─ InvoiceExportView.axaml
│  │  └─ StatisticsView.axaml
│  ├─ ViewModels/              # 视图模型（MVVM）
│  │  ├─ MainWindowViewModel.cs
│  │  ├─ InvoiceImportViewModel.cs
│  │  ├─ InvoiceExportViewModel.cs
│  │  └─ StatisticsViewModel.cs
│  ├─ Models/                  # 领域模型
│  │  ├─ InvoiceInfo.cs
│  │  ├─ ArchiveItem.cs
│  │  ├─ StatisticsData.cs
│  │  ├─ HeatmapDayData.cs
│  │  ├─ AppSettings.cs
│  │  └─ LogEntry.cs
│  ├─ Services/                # 业务服务
│  │  ├─ OcrService.cs
│  │  ├─ PdfService.cs
│  │  ├─ FileManagerService.cs
│  │  ├─ StatisticsService.cs
│  │  ├─ SettingsService.cs
│  │  └─ LoggerService.cs
│  ├─ Converters/              # UI 转换器
│  ├─ Styles/                  # 应用样式与主题
│  └─ ViewLocator.cs           # View 与 ViewModel 的关联
├─ archive_data/               # 默认归档目录（按 YYYY-MM 子目录）
├─ temp_uploads/               # 默认临时上传目录
├─ LabInvoiceSystem.sln
└─ start.bat                   # Windows 启动脚本
```

### 架构要点

- **MVVM 模式**  
  - `Views` 只关心界面布局与绑定。  
  - `ViewModels` 实现具体业务逻辑与命令（`RelayCommand`）。  
  - `Models` 承载领域数据（发票、统计、配置等）。  
  - `Services` 负责与外部系统和底层资源的交互（文件系统、网络、PDF、统计等）。

- **导航逻辑**  
  - `MainWindowViewModel` 中维护 `CurrentView` 和 `CurrentViewName`，通过 `Navigate` 命令在 Import / Export / Statistics 之间切换。  
  - 部分 ViewModel 实现 `INavigable` 接口，在 `OnNavigatedTo` 中执行数据刷新逻辑（如导出列表、统计数据）。

- **服务职责概览**  
  - `OcrService`：  
    - 负责管理 Baidu OCR `access_token` 的获取与缓存。  
    - 将图片（或由 PDF 转换得到的图片）发送到百度增值税发票 OCR 接口，并解析返回 JSON。  
  - `PdfService`：  
    - 使用 `PDFtoImage` 与 `SkiaSharp` 将 PDF 的第一页转换成 PNG 图像。  
    - 对 PDF 文件头和文件大小进行基本校验，避免处理损坏或伪造文件。  
  - `FileManagerService`：  
    - 保存上传文件到临时目录。  
    - 按统一命名规则进行归档，维护 `archive_data/YYYY-MM` 目录结构。  
    - 导出 ZIP、删除临时或归档文件。  
  - `StatisticsService`：  
    - 基于 `ArchiveItem` 计算总金额、总数量、每日金额、近 30 天金额等统计数据。  
  - `SettingsService`：  
    - 全局管理 `AppSettings` 的加载与保存。  
  - `LoggerService`：  
    - 将关键操作追加写入 JSON 日志文件，便于审计与追踪。

---

## 技术栈与依赖

本项目基于 .NET 8 与 Avalonia，主要依赖如下（来自 `LabInvoiceSystem.csproj`）：

- **UI 框架**  
  - `Avalonia` 11.3.9  
  - `Avalonia.Desktop` 11.3.9  
  - `Avalonia.Themes.Fluent` 11.3.9  
  - `Avalonia.Fonts.Inter` 11.3.9  
  - `Svg.Controls.Skia.Avalonia` 11.3.6.2：用于渲染 SVG 图标与资源。

- **MVVM 支持**  
  - `CommunityToolkit.Mvvm` 8.2.1

- **控件与图表**  
  - `Avalonia.Controls.DataGrid` 11.3.9：用于表格展示归档发票等。  
  - `LiveChartsCore.SkiaSharpView.Avalonia` 2.0.0-rc4：用于可视化图表展示（如统计界面）。

- **数据处理与导出**  
  - `MiniExcel` 1.42.0：轻量级表格处理 / 导出组件（可用于 Excel 导出等场景）。

- **PDF 与图像处理**  
  - `PDFtoImage` 4.1.0：将 PDF 转换为图像文件。  
  - `SkiaSharp` 2.88.9：图像渲染与处理。

- **其他**  
  - .NET BCL 中的 `HttpClient`、`System.Text.Json` 等，用于调用百度 OCR API 与处理 JSON 数据。

---

## 常见问题 FAQ

### 1. OCR 调用失败怎么办？

可能原因：

- 网络不可用或访问百度 OCR 接口受限。  
- `BaiduAppId` / `BaiduApiKey` / `BaiduSecretKey` 配置不正确或已过期。  
- 请求频率过高触发限流。

建议排查步骤：

- 检查本机网络环境并尝试通过浏览器访问百度 AI 平台。  
- 查看 `%APPDATA%/LabInvoiceSystem/appsettings.json` 中的凭据是否填写正确。  
- 检查应用输出的错误信息（控制台或 `error.log`）。

### 2. PDF 转图片失败怎么办？

可能原因：

- 上传的文件不是合法 PDF（文件头不是 `%PDF-`）。  
- PDF 文件为空或损坏。  
- `PDFtoImage` 或 `SkiaSharp` 组件运行异常。

建议排查步骤：

- 确认上传的文件在其他 PDF 阅读器中可以正常打开。  
- 检查错误提示中是否包含“文件不是有效的 PDF 格式”或“PDF 转换未生成输出图片文件”等信息。  
- 如定位到个别问题文件，可暂时跳过该文件并手动录入发票信息。

### 3. 为什么统计界面没有数据显示？

- 确认是否已经成功对发票执行“确认并归档”操作。  
- 确认 `archive_data` 目录下确实存在归档文件。  
- 在统计界面点击“刷新数据”按钮以重新加载最新归档文件。

### 4. 日志和配置文件存在哪？

- 配置文件：`%APPDATA%/LabInvoiceSystem/appsettings.json`。  
- 操作日志：`%APPDATA%/LabInvoiceSystem/upload_logs.json`。

### 5. 如何查看和控制 Baidu OCR 月调用次数？

- 在统计面板中查看顶部状态区域的“本月调用: X/Y”，其中 `Y` 来自 `AppSettings.BaiduMonthlyQuota`。  
- 如需调整配额上限，可在 `%APPDATA%/LabInvoiceSystem/appsettings.json` 中修改 `BaiduMonthlyQuota`，使其与你在百度控制台购买的套餐保持一致。  
- 当实际调用接近或超过百度接口限制时，可能会在 OCR 调用失败提示中看到限流相关错误信息，可结合前述排查步骤处理。  

---

## License 与致谢

- **License**：本项目以 **MIT License** 开源，你可以在遵守 MIT 许可条款的前提下自由使用、修改和分发本项目。  
- **致谢**：  
  - [Avalonia UI](https://avaloniaui.net/)  
  - [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)  
  - [LiveChartsCore](https://github.com/beto-rodriguez/LiveCharts2)  
  - [MiniExcel](https://github.com/shps951023/MiniExcel)  
  - [PDFtoImage](https://github.com/roryprimrose/PDFtoImage)  
  - [SkiaSharp](https://github.com/mono/SkiaSharp)  
  - 百度智能云 OCR 服务
