using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public async Task OnNavigatedTo()
        {
            await LoadArchivesAsync();
        }

        [ObservableProperty]
        private ObservableCollection<DateGroup> _dateGroups = new();

        [ObservableProperty]
        private List<ArchiveItem> _selectedItems = new();

        [ObservableProperty]
        private string _statusMessage = "准备就绪";

        [ObservableProperty]
        private bool _isProcessing;

        public InvoiceExportViewModel()
        {
            _fileManager = new FileManagerService();
            _logger = new LoggerService();
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
                var archives = _fileManager.GetArchivedInvoices();
                
                // 按日期（YYYY-MM-DD）分组
                var grouped = archives
                    .GroupBy(a => a.Date)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new DateGroup
                    {
                        Date = g.Key,
                        Invoices = new ObservableCollection<ArchiveItem>(g.OrderBy(i => i.FileName))
                    });

                DateGroups.Clear();
                foreach (var group in grouped)
                {
                    DateGroups.Add(group);
                }

                StatusMessage = $"已加载 {archives.Count} 个归档发票";
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
        private async Task ExportDateAsync(string? date)
        {
            if (string.IsNullOrEmpty(date)) return;

            IsProcessing = true;
            StatusMessage = $"正在导出 {date} 的发票...";

            try
            {
                var group = DateGroups.FirstOrDefault(g => g.Date == date);
                if (group == null || group.Invoices.Count == 0)
                {
                    StatusMessage = "没有可导出的发票";
                    return;
                }

                var filePaths = group.Invoices.Select(i => i.FilePath).ToList();
                
                // 智能生成ZIP文件名
                var zipFileName = GenerateZipFileName(group);
                var zipPath = await _fileManager.ExportInvoicesToZipAsync(filePaths, zipFileName);
                _logger.LogExport($"导出 {date} 共 {group.Invoices.Count} 张发票 => {zipFileName}");

                // 打开文件夹
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{zipPath}\"");

                StatusMessage = $"已导出 {group.Invoices.Count} 张发票到 {zipFileName}";
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

        private string GenerateZipFileName(DateGroup group)
        {
            // 获取该日期的所有支付方式
            var paymentMethods = group.Invoices
                .Select(i => i.InvoiceInfo?.PaymentMethod)
                .Where(pm => !string.IsNullOrEmpty(pm))
                .Distinct()
                .ToList();

            var dateStr = group.Date.Replace("-", ""); // YYYYMMDD

            if (paymentMethods.Count == 1)
            {
                // 单一支付方式：YYYYMMDD+支付方式.zip
                return $"{dateStr}+{paymentMethods[0]}.zip";
            }
            else
            {
                // 多种支付方式或无支付方式：YYYYMMDD_发票.zip
                return $"{dateStr}_发票.zip";
            }
        }

        [RelayCommand]
        private async Task DeleteInvoiceAsync(ArchiveItem? item)
        {
            if (item == null) return;

            IsProcessing = true;
            StatusMessage = $"正在删除 {item.FileName}...";

            try
            {
                await _fileManager.DeleteArchivedFileAsync(item.FilePath);
                _logger.LogDelete($"删除文件: {item.FileName}");

                // 从DateGroups中移除该项
                foreach (var group in DateGroups)
                {
                    if (group.Invoices.Contains(item))
                    {
                        group.Invoices.Remove(item);
                        
                        // 如果该日期组没有发票了，移除整个组
                        if (group.Invoices.Count == 0)
                        {
                            DateGroups.Remove(group);
                        }
                        break;
                    }
                }

                StatusMessage = "删除成功";
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task DeleteDateGroupAsync(string? date)
        {
            if (string.IsNullOrEmpty(date)) return;

            var group = DateGroups.FirstOrDefault(g => g.Date == date);
            if (group == null || group.Invoices.Count == 0)
            {
                StatusMessage = "没有可删除的发票";
                return;
            }

            IsProcessing = true;
            StatusMessage = $"正在删除 {date} 的所有发票...";

            try
            {
                var filePaths = group.Invoices.Select(i => i.FilePath).ToList();
                await _fileManager.DeleteArchivedFilesAsync(filePaths);
                _logger.LogDelete($"批量删除 {date} 的 {filePaths.Count} 个文件");

                // 从DateGroups中移除该组
                DateGroups.Remove(group);

                StatusMessage = $"已删除 {filePaths.Count} 张发票";
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败: {ex.Message}";
                // 即使部分删除失败，也刷新列表
                await LoadArchivesAsync();
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
