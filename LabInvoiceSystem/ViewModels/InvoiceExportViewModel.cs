using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabInvoiceSystem.Models;
using LabInvoiceSystem.Services;

namespace LabInvoiceSystem.ViewModels
{
    public partial class InvoiceExportViewModel : ViewModelBase, INavigable
    {
        private readonly FileManagerService _fileManager;
        private readonly LoggerService _logger;
        private ArchiveItem? _pendingDeleteItem;
        private DateGroup? _pendingDeleteGroup;
        private readonly List<ArchiveItem> _allArchives = new();
        private ArchiveItem? _editingItem;

        public async Task OnNavigatedTo()
        {
            await LoadArchivesAsync();
        }

        [ObservableProperty]
        private ObservableCollection<DateGroup> _dateGroups = new();

        [ObservableProperty]
        private string _statusMessage = "准备就绪";

        [ObservableProperty]
        private string _exportDirectory = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _emptyStateText = "暂无归档数据";

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private bool _isDeleteConfirmationOpen;

        [ObservableProperty]
        private string _deleteConfirmationTitle = string.Empty;

        [ObservableProperty]
        private string _deleteConfirmationMessage = string.Empty;

        [ObservableProperty]
        private bool _isEditOpen;

        [ObservableProperty]
        private DateTimeOffset _editInvoiceDate = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset _editEntryDate = DateTimeOffset.Now;

        [ObservableProperty]
        private decimal _editAmount;

        [ObservableProperty]
        private string _editItemName = string.Empty;

        [ObservableProperty]
        private string _editPaymentMethod = "公务卡";

        [ObservableProperty]
        private string _editInvoiceNumber = string.Empty;

        [ObservableProperty]
        private string _editSellerName = string.Empty;

        [ObservableProperty]
        private string _editSellerTaxId = string.Empty;

        [ObservableProperty]
        private string _editError = string.Empty;

        public bool AreAllVisibleSelected
        {
            get
            {
                var visibleInvoices = DateGroups.SelectMany(group => group.Invoices).ToList();
                return visibleInvoices.Count > 0 && visibleInvoices.All(item => item.IsSelected);
            }
        }

        public string SelectionToggleText => AreAllVisibleSelected ? "取消全选" : "全选结果";

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        public InvoiceExportViewModel()
        {
            _fileManager = new FileManagerService();
            _logger = new LoggerService();

            var settings = SettingsService.Instance.Settings;
            _exportDirectory = settings.ExportDirectory;
        }
        
        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadArchivesAsync();
        }

        [RelayCommand]
        private async Task LoadArchivesAsync()
        {
            IsProcessing = true;
            StatusMessage = "正在加载归档列表...";

            try
            {
                var archives = await _fileManager.GetArchivedInvoicesAsync();
                foreach (var item in _allArchives)
                    item.PropertyChanged -= OnArchiveItemPropertyChanged;
                _allArchives.Clear();
                _allArchives.AddRange(archives);
                foreach (var item in _allArchives)
                    item.PropertyChanged += OnArchiveItemPropertyChanged;
                ApplyFilter();
                StatusMessage = $"已加载 {archives.Count} 张归档发票";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }
        
        [RelayCommand]
        private async Task DownloadInvoiceAsync(ArchiveItem? item)
        {
            await DownloadFileAsync(item);
        }

        [RelayCommand]
        private async Task DownloadFileAsync(ArchiveItem? item)
        {
            if (item == null) return;

            try
            {
                var topLevel = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (topLevel == null) return;

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "保存发票文件",
                    SuggestedFileName = item.FileName,
                    DefaultExtension = System.IO.Path.GetExtension(item.FileName)
                });

                if (file != null)
                {
                    await using var sourceStream = System.IO.File.OpenRead(item.FilePath);
                    await using var destStream = await file.OpenWriteAsync();
                    await sourceStream.CopyToAsync(destStream);

                    StatusMessage = "文件已保存";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"下载失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportDateAsync(DateGroup? group)
        {
            if (group == null) return;
            var archives = group?.Invoices.Where(i => i.IsSelected).ToList();
            if (archives == null || archives.Count == 0)
            {
                StatusMessage = "请至少选择一张发票";
                return;
            }

            await ExportArchivesAsync(archives);
        }

        [RelayCommand]
        private void ToggleSelection()
        {
            var visibleInvoices = DateGroups.SelectMany(group => group.Invoices).ToList();
            if (visibleInvoices.Count == 0) return;

            var shouldSelect = !AreAllVisibleSelected;
            foreach (var item in visibleInvoices)
                item.IsSelected = shouldSelect;

            StatusMessage = shouldSelect
                ? $"已选择当前 {visibleInvoices.Count} 张发票"
                : $"已取消当前 {visibleInvoices.Count} 张发票";
        }

        [RelayCommand]
        private async Task ExportSelectedAsync()
        {
            var selectedGroups = DateGroups.Where(g => g.SelectedCount > 0).ToList();
            var archives = selectedGroups.SelectMany(g => g.Invoices.Where(i => i.IsSelected)).ToList();
            if (archives.Count == 0)
            {
                StatusMessage = "请至少选择一张发票";
                return;
            }

            await ExportArchivesAsync(archives);
        }

        private async Task ExportArchivesAsync(List<ArchiveItem> archives)
        {
            var orderedDates = archives
                .Select(item => item.InvoiceInfo.InvoiceDate.ToString("yyyy-MM-dd"))
                .Distinct()
                .OrderBy(date => date)
                .ToList();
            var dateToken = orderedDates.Count == 1
                ? orderedDates[0].Replace("-", "")
                : $"{orderedDates[0].Replace("-", "")}-{orderedDates[^1].Replace("-", "")}";

            IsProcessing = true;
            StatusMessage = $"正在导出 {archives.Count} 张发票...";

            try
            {
                // 智能生成ZIP文件名
                var zipFileName = GenerateZipFileName(dateToken, archives);

                var excelFileName = $"{dateToken}_报账发票明细.xlsx";

                var zipPath = await _fileManager.ExportInvoicesToZipWithExcelAsync(archives, zipFileName, excelFileName);
                _logger.LogExport($"导出 {archives.Count} 张发票 => {zipFileName}");

                RevealExportedFile(zipPath);

                StatusMessage = $"已导出 {archives.Count} 张发票到 {zipFileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task SetExportDirectoryAsync()
        {
            try
            {
                var topLevel = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (topLevel == null)
                {
                    return;
                }

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择导出文件夹",
                    AllowMultiple = false
                });

                var folder = folders?.FirstOrDefault();
                if (folder == null)
                {
                    return;
                }

                var path = folder.Path?.LocalPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    StatusMessage = "无法获取所选文件夹路径";
                    return;
                }

                var settings = SettingsService.Instance.Settings;
                var previousPath = settings.ExportDirectory;
                settings.ExportDirectory = path;
                if (!SettingsService.Instance.SaveSettings())
                {
                    settings.ExportDirectory = previousPath;
                    StatusMessage = "导出路径保存失败，请检查配置目录权限";
                    return;
                }

                ExportDirectory = path;
                StatusMessage = $"导出路径已设置为: {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置导出路径失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenExportFolder()
        {
            try
            {
                var settings = SettingsService.Instance.Settings;
                var exportDir = settings.ExportDirectory;

                if (string.IsNullOrWhiteSpace(exportDir))
                {
                    exportDir = Path.Combine(Path.GetTempPath(), "LabInvoiceExport");
                }

                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                Process.Start(new ProcessStartInfo(exportDir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开导出文件夹失败: {ex.Message}";
            }
        }

        private string GenerateZipFileName(string dateToken, IEnumerable<ArchiveItem> archives)
        {
            // 获取该日期的所有支付方式
            var paymentMethods = archives
                .Select(i => i.InvoiceInfo?.PaymentMethod)
                .Where(pm => !string.IsNullOrEmpty(pm))
                .Distinct()
                .ToList();

            if (paymentMethods.Count == 1)
            {
                // 单一支付方式：YYYYMMDD+支付方式.zip
                return $"{dateToken}+{paymentMethods[0]}.zip";
            }
            else
            {
                // 多种支付方式或无支付方式：YYYYMMDD_发票.zip
                return $"{dateToken}_发票.zip";
            }
        }

        [RelayCommand]
        private void DeleteInvoice(ArchiveItem? item)
        {
            if (item == null) return;

            _pendingDeleteItem = item;
            _pendingDeleteGroup = null;
            DeleteConfirmationTitle = OperatingSystem.IsWindows() ? "移到回收站？" : "永久删除？";
            var deleteNotice = OperatingSystem.IsWindows()
                ? "将移到 Windows 回收站，可在回收站中恢复"
                : "将被永久删除，无法恢复";
            DeleteConfirmationMessage =
                $"{item.FileName} 及其归档信息{deleteNotice}。\n录入日期: {item.InvoiceInfo?.EntryDate:yyyy-MM-dd}\n购买日期: {item.InvoiceInfo?.InvoiceDate:yyyy-MM-dd}\n金额: ¥{item.InvoiceInfo?.Amount:F2}";
            IsDeleteConfirmationOpen = true;
        }

        [RelayCommand]
        private void DeleteDateGroup(DateGroup? group)
        {
            if (group == null || group.Invoices.Count == 0)
            {
                StatusMessage = "没有可删除的发票";
                return;
            }

            _pendingDeleteItem = null;
            _pendingDeleteGroup = group;
            var entryDate = group.Invoices[0].InvoiceInfo.EntryDate.ToString("yyyy-MM-dd");
            DeleteConfirmationTitle = OperatingSystem.IsWindows() ? "将该日期组移到回收站？" : "永久删除该日期组？";
            var deleteNotice = OperatingSystem.IsWindows()
                ? "将移到 Windows 回收站，可在回收站中恢复"
                : "将被永久删除，无法恢复";
            DeleteConfirmationMessage =
                $"录入日期 {entryDate}、购买日期 {group.Date} 下的 {group.TotalCount} 张发票及归档信息{deleteNotice}。\n总金额: ¥{group.TotalAmount:F2}";
            IsDeleteConfirmationOpen = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            ClearPendingDelete();
            StatusMessage = "已取消删除";
        }

        [RelayCommand]
        private async Task ConfirmDeleteAsync()
        {
            var item = _pendingDeleteItem;
            var group = _pendingDeleteGroup;
            ClearPendingDelete();

            if (item != null)
            {
                await DeleteArchivedItemAsync(item);
            }
            else if (group != null)
            {
                await DeleteArchivedGroupAsync(group);
            }
        }

        private static void RevealExportedFile(string filePath)
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText.Trim();
            var filtered = string.IsNullOrEmpty(query)
                ? _allArchives
                : _allArchives.Where(item => MatchesSearch(item, query)).ToList();

            var entryExpansion = DateGroups.ToDictionary(group => group.Date, group => group.IsExpanded);
            var purchaseExpansion = DateGroups
                .SelectMany(entryGroup => entryGroup.PurchaseDateGroups.Select(purchaseGroup => new
                {
                    Key = (entryGroup.Date, purchaseGroup.Date),
                    purchaseGroup.IsExpanded
                }))
                .ToDictionary(item => item.Key, item => item.IsExpanded);

            var grouped = filtered
                .GroupBy(item => item.InvoiceInfo.EntryDate.Date)
                .OrderByDescending(group => group.Key)
                .Select(entryGroup =>
                {
                    var entryDate = entryGroup.Key.ToString("yyyy-MM-dd");
                    var invoices = entryGroup.OrderBy(item => item.FileName).ToList();
                    var purchaseGroups = invoices
                        .GroupBy(item => item.InvoiceInfo.InvoiceDate.Date)
                        .OrderByDescending(group => group.Key)
                        .Select(purchaseGroup =>
                        {
                            var purchaseDate = purchaseGroup.Key.ToString("yyyy-MM-dd");
                            return new DateGroup
                            {
                                Date = purchaseDate,
                                IsExpanded = purchaseExpansion.GetValueOrDefault((entryDate, purchaseDate), true),
                                Invoices = new ObservableCollection<ArchiveItem>(purchaseGroup.OrderBy(item => item.FileName))
                            };
                        });

                    return new DateGroup
                    {
                        Date = entryDate,
                        IsExpanded = entryExpansion.GetValueOrDefault(entryDate, true),
                        Invoices = new ObservableCollection<ArchiveItem>(invoices),
                        PurchaseDateGroups = new ObservableCollection<DateGroup>(purchaseGroups)
                    };
                });

            foreach (var entryGroup in DateGroups)
            {
                entryGroup.Invoices.Clear();
                foreach (var purchaseGroup in entryGroup.PurchaseDateGroups)
                    purchaseGroup.Invoices.Clear();
            }
            DateGroups.Clear();
            foreach (var group in grouped)
                DateGroups.Add(group);

#if DEBUG
            var groupedInvoices = DateGroups.SelectMany(group => group.PurchaseDateGroups)
                .SelectMany(group => group.Invoices).ToList();
            Debug.Assert(groupedInvoices.Count == filtered.Count && groupedInvoices.Distinct().Count() == groupedInvoices.Count,
                "Every visible invoice must appear in exactly one purchase-date group.");
#endif

            EmptyStateText = _allArchives.Count == 0
                ? "暂无归档数据"
                : $"没有找到“{query}”";
            NotifySelectionStateChanged();
        }

        private void OnArchiveItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ArchiveItem.IsSelected))
                NotifySelectionStateChanged();
        }

        private void NotifySelectionStateChanged()
        {
            OnPropertyChanged(nameof(AreAllVisibleSelected));
            OnPropertyChanged(nameof(SelectionToggleText));
        }

        private static bool MatchesSearch(ArchiveItem item, string query)
        {
            var info = item.InvoiceInfo;
            var values = new[]
            {
                item.FileName, item.Date, info.EntryDate.ToString("yyyy-MM-dd"),
                info.InvoiceDate.ToString("yyyy-MM-dd"), info.ItemName, info.PaymentMethod,
                info.InvoiceNumber, info.SellerName, info.SellerTaxId,
                info.Amount.ToString("0.##")
            };
            return values.Any(value => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
        }

        [RelayCommand]
        private void OpenInvoice(ArchiveItem? item)
        {
            if (item == null) return;
            try
            {
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                StatusMessage = $"已打开 {item.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private void EditInvoice(ArchiveItem? item)
        {
            if (item == null) return;
            _editingItem = item;
            var info = item.InvoiceInfo;
            EditEntryDate = new DateTimeOffset(info.EntryDate);
            EditInvoiceDate = new DateTimeOffset(info.InvoiceDate);
            EditAmount = info.Amount;
            EditItemName = info.ItemName;
            EditPaymentMethod = info.PaymentMethod;
            EditInvoiceNumber = info.InvoiceNumber;
            EditSellerName = info.SellerName;
            EditSellerTaxId = info.SellerTaxId;
            EditError = string.Empty;
            IsEditOpen = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            _editingItem = null;
            EditError = string.Empty;
            IsEditOpen = false;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (_editingItem == null) return;
            if (EditAmount <= 0)
            {
                EditError = "金额必须大于 0";
                return;
            }
            if (string.IsNullOrWhiteSpace(EditItemName))
            {
                EditError = "请填写项目名称";
                return;
            }

            IsProcessing = true;
            EditError = string.Empty;
            var info = _editingItem.InvoiceInfo;
            var originalEntryDate = info.EntryDate;
            var originalDate = info.InvoiceDate;
            var originalAmount = info.Amount;
            var originalItemName = info.ItemName;
            var originalPaymentMethod = info.PaymentMethod;
            var originalInvoiceNumber = info.InvoiceNumber;
            var originalSellerName = info.SellerName;
            var originalSellerTaxId = info.SellerTaxId;
            try
            {
                var item = _editingItem;
                info.EntryDate = EditEntryDate.Date;
                info.InvoiceDate = EditInvoiceDate.DateTime;
                info.Amount = EditAmount;
                info.ItemName = EditItemName.Trim();
                info.PaymentMethod = EditPaymentMethod.Trim();
                info.InvoiceNumber = EditInvoiceNumber.Trim();
                info.SellerName = EditSellerName.Trim();
                info.SellerTaxId = EditSellerTaxId.Trim();
                await _fileManager.UpdateArchivedMetadataAsync(item);
                IsEditOpen = false;
                _editingItem = null;
                ApplyFilter();
                StatusMessage = originalEntryDate.Date == info.EntryDate.Date
                    ? $"已保存 {item.FileName} 的归档信息"
                    : $"已将 {item.FileName} 移动到 {info.EntryDate:yyyy-MM-dd} 日期组";
            }
            catch (Exception ex)
            {
                info.EntryDate = originalEntryDate;
                info.InvoiceDate = originalDate;
                info.Amount = originalAmount;
                info.ItemName = originalItemName;
                info.PaymentMethod = originalPaymentMethod;
                info.InvoiceNumber = originalInvoiceNumber;
                info.SellerName = originalSellerName;
                info.SellerTaxId = originalSellerTaxId;
                EditError = $"保存失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task DeleteArchivedItemAsync(ArchiveItem item)
        {
            IsProcessing = true;
            StatusMessage = $"正在删除 {item.FileName}...";

            try
            {
                await _fileManager.DeleteArchivedFileAsync(item.FilePath);
                _logger.LogDelete($"删除文件: {item.FileName}");

                item.PropertyChanged -= OnArchiveItemPropertyChanged;
                _allArchives.Remove(item);
                ApplyFilter();
                StatusMessage = "已移到回收站";
            }
            catch (Exception ex)
            {
                var errorMessage = $"删除失败: {ex.Message}";
                await LoadArchivesAsync();
                StatusMessage = errorMessage;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task DeleteArchivedGroupAsync(DateGroup group)
        {
            IsProcessing = true;
            var entryDate = group.Invoices[0].InvoiceInfo.EntryDate.ToString("yyyy-MM-dd");
            StatusMessage = $"正在删除录入日期 {entryDate}、购买日期 {group.Date} 的发票...";

            try
            {
                var filePaths = group.Invoices.Select(i => i.FilePath).ToList();
                await _fileManager.DeleteArchivedFilesAsync(filePaths);
                _logger.LogDelete($"批量删除录入日期 {entryDate}、购买日期 {group.Date} 的 {filePaths.Count} 个文件");

                foreach (var item in group.Invoices.ToList())
                {
                    item.PropertyChanged -= OnArchiveItemPropertyChanged;
                    _allArchives.Remove(item);
                }
                ApplyFilter();

                StatusMessage = $"已将 {filePaths.Count} 张发票移到回收站";
            }
            catch (Exception ex)
            {
                var errorMessage = $"删除失败: {ex.Message}";
                await LoadArchivesAsync();
                StatusMessage = errorMessage;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ClearPendingDelete()
        {
            _pendingDeleteItem = null;
            _pendingDeleteGroup = null;
            IsDeleteConfirmationOpen = false;
            DeleteConfirmationTitle = string.Empty;
            DeleteConfirmationMessage = string.Empty;
        }
    }
}
