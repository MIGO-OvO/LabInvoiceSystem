# 更新日志

## 2025-11-21 - 数据导出功能重构

### 更新内容

按照Web端项目（Lab-Invoice-Auto）的设计理念，将导出功能从**按月导出**重构为**按日期导出**。

### 主要变更

#### 1. 新增模型
- **DateGroup.cs** - 按日期分组的模型
  - `Date`：具体日期（YYYY-MM-DD格式）
  - `Invoices`：该日期下的所有发票
  - `TotalCount`：发票总数
  - `TotalAmount`：总金额

#### 2. 修改的文件

**Models/ArchiveItem.cs**
- 新增 `Date` 属性（YYYY-MM-DD格式）
- 保留 `YearMonth` 属性用于向后兼容

**Services/FileManagerService.cs**
- `GetArchivedInvoices()` 方法现在会自动设置每个归档项的 `Date` 属性
- 从 `InvoiceInfo.InvoiceDate` 解析并格式化为 YYYY-MM-DD

**ViewModels/InvoiceExportViewModel.cs**
- 删除 `MonthGroup` 类，使用新的 `DateGroup` 模型
- 将 `ArchivedGroups` 重命名为 `DateGroups`
- `LoadArchivesAsync()` 现在按日期（YYYY-MM-DD）分组
- `ExportMonthAsync()` 重命名为 `ExportDateAsync()`
- 新增 `GenerateZipFileName()` 方法，实现智能命名：
  - 单一支付方式：`YYYYMMDD+支付方式.zip`（如：`20251121+公务卡.zip`）
  - 多种支付方式：`YYYYMMDD_发票.zip`（如：`20251121_发票.zip`）

**Views/InvoiceExportView.axaml**
- 更新数据绑定从 `ArchivedGroups` 到 `DateGroups`
- 分组标题显示具体日期（YYYY-MM-DD）而非年月
- 增加总金额显示
- 按钮文本从"导出 Excel"改为"导出为 ZIP"
- 页面描述从"按月归档的发票数据"改为"按日期归档的发票数据"

### 功能优化

#### 导出逻辑
- **旧逻辑**：按月份导出整个月的所有发票
- **新逻辑**：按具体日期导出，每天的发票独立打包

#### ZIP文件命名
- **单一支付方式示例**：`20251121+公务卡.zip`
- **混合支付方式示例**：`20251121_发票.zip`
- 文件名更加简洁明确，便于识别和管理

#### UI改进
- 按日期分组显示，结构更清晰
- 每个日期组显示发票数量和总金额
- 支持按天单独导出，灵活性更高

### 参考项目
本次重构参考了Web端项目的设计：
- GitHub: https://github.com/MIGO-OvO/Lab-Invoice-Auto
- 归档结构：`archive_data/YYYY-MM/YYYYMMDD-项目名称-支付方式-金额元.pdf`
- 导出策略：支持按日期、按日期范围、按选中文件导出

### 构建状态
✅ 编译成功，无错误
⚠️ 5个警告（均为非关键性警告，不影响功能）

---

## 2025-11-21 - 修复中文显示乱码与增加删除功能

### 更新内容

解决统计页面"公务卡"显示乱码问题，并在导出页面增加删除功能，方便用户清理已报账的发票文件。

### 主要变更

#### 1. 修复统计页面中文乱码

**问题**：统计分析页面的饼图中，"公务卡"等中文标签显示为乱码。

**原因**：LiveCharts的饼图使用SkiaSharp渲染，默认未配置DataLabelsFormatter导致中文无法正确显示。

**解决方案**：
- 在`StatisticsViewModel.GeneratePaymentPieChart()`中添加`DataLabelsFormatter`
- 格式化器配置：`point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue}张"`
- 确保饼图标签正确显示中文，格式为"支付方式: 数量张"

**修改文件**：
- `ViewModels/StatisticsViewModel.cs`

#### 2. 增加导出页面删除功能

**功能描述**：
- **单个删除**：用户可以删除列表中的单个发票文件
- **批量删除**：用户可以一键删除整天的所有发票文件

**实现细节**：

**A. 服务层（FileManagerService.cs）**
- 新增 `DeleteArchivedFileAsync(string filePath)` - 删除单个归档文件
- 新增 `DeleteArchivedFilesAsync(List<string> filePaths)` - 批量删除归档文件
- 删除失败时提供详细错误信息

**B. ViewModel层（InvoiceExportViewModel.cs）**
- 新增 `DeleteInvoiceCommand` - 删除单个发票命令
  - 删除文件后自动从列表中移除
  - 如果日期组为空则自动移除整个分组
- 新增 `DeleteDateGroupCommand` - 删除整个日期组命令
  - 批量删除该日期的所有发票
  - 删除成功后从列表中移除整个日期组

**C. UI层（InvoiceExportView.axaml）**
- 在日期组标题右侧添加"删除整天"按钮（红色警告样式）
- 在DataGrid操作列添加删除图标按钮
- 操作列包含两个按钮：
  - 下载按钮（保持原有功能）
  - 删除按钮（新增，红色图标）
- 所有按钮都添加了ToolTip提示

**UI布局**：
```
日期组标题区域：
[日期信息] ----------------------- [导出为ZIP] [删除整天]

发票列表操作列：
[下载按钮] [删除按钮]
```

### 用户体验改进

1. **中文显示正常**：统计页面饼图中的"公务卡"、"现金"等标签现在正确显示
2. **快速清理**：报账完成后可快速删除已处理的发票
3. **灵活操作**：支持单个删除和批量删除两种方式
4. **视觉反馈**：删除按钮使用红色警告样式，防止误操作
5. **操作提示**：按钮带有ToolTip，清晰说明功能

### 构建状态
✅ 编译成功，无错误
⚠️ 5个警告（均为非关键性警告，不影响功能）

---

## 2025-11-21 - 修复UI显示问题与完善日志记录

### 问题修复

#### 1. 修复DataGrid不显示问题

**问题描述**：导出页面的发票列表（DataGrid）没有正常显示，只能看到日期组标题，看不到下方的发票明细表格。

**原因分析**：DataGrid缺少必要的属性配置，导致表格高度为0或被折叠。

**解决方案**：
- 添加 `AutoGenerateColumns="False"` 确保使用手动定义的列
- 添加 `MinHeight="100"` 设置最小高度100像素
- 添加 `MaxHeight="400"` 设置最大高度400像素，超出部分可滚动

**修改文件**：`Views/InvoiceExportView.axaml`

#### 2. 完善中文字体支持

**问题描述**：统计页面饼图中的中文标签"公务卡"、"现金"等仍显示为乱码或方框。

**根本原因**：虽然已配置DataLabelsFormatter，但DataLabelsPaint缺少字体设置。

**解决方案**：
- 为 `DataLabelsPaint` 添加 `SKTypeface` 配置
- 使用 `SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Normal)` 设置微软雅黑字体
- 确保中文字符正确渲染

**修改文件**：`ViewModels/StatisticsViewModel.cs`

#### 3. 完善删除操作日志记录

**问题描述**：用户执行删除操作时，日志系统没有记录相关操作。

**解决方案**：
- 在 `InvoiceExportViewModel` 添加 `LoggerService` 依赖
- 在 `DeleteInvoiceAsync` 方法中记录单个删除操作：`"删除文件: [文件名]"`
- 在 `DeleteDateGroupAsync` 方法中记录批量删除操作：`"批量删除 [日期] 的 [数量] 个文件"`

**修改文件**：`ViewModels/InvoiceExportViewModel.cs`

### 用户体验改进

1. **发票列表正常显示**：现在可以看到完整的发票明细表格
2. **中文标签清晰显示**：饼图中的支付方式标签不再乱码
3. **操作可追溯**：所有删除操作都会被记录到日志中
4. **表格自适应高度**：最小100px，最大400px，内容多时可滚动

### 构建状态
✅ 编译成功，无错误
⚠️ 5个警告（均为非关键性警告，不影响功能）
