using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabInvoiceSystem.Models;
using LabInvoiceSystem.Services;

namespace LabInvoiceSystem.ViewModels
{
    public partial class InvoiceImportViewModel : ViewModelBase
    {
        private readonly OcrService _ocrService;
        private readonly FileManagerService _fileManager;
        private readonly LoggerService _logger;
        private readonly PdfService _pdfService;

        [ObservableProperty]
        private ObservableCollection<InvoiceInfo> _uploadedInvoices = new();

        [ObservableProperty]
        private InvoiceInfo? _selectedInvoice;

        [ObservableProperty]
        private byte[]? _previewImageBytes;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = "准备就绪";

        [ObservableProperty]
        private DateTimeOffset _uniformUploadDate = DateTimeOffset.Now;

        public InvoiceImportViewModel()
        {
            _ocrService = new OcrService();
            _fileManager = new FileManagerService();
            _logger = new LoggerService();
            _pdfService = new PdfService();
        }

        partial void OnSelectedInvoiceChanged(InvoiceInfo? value)
        {
            if (value != null && File.Exists(value.FilePath))
            {
                _ = LoadPreviewImageAsync(value.FilePath);
            }
            else
            {
                PreviewImageBytes = null;
            }
        }

        [RelayCommand]
        private void SetPaymentMethod(string method)
        {
            if (SelectedInvoice != null)
            {
                SelectedInvoice.PaymentMethod = method;
            }
        }

        [RelayCommand]
        private async Task UploadFilesAsync(IEnumerable<IStorageFile>? files)
        {
            if (files == null)
            {
                try
                {
                    var app = Application.Current;
                    if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                        desktop.MainWindow is not null)
                    {
                        var pickerFiles = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(
                            new FilePickerOpenOptions
                            {
                                Title = "选择发票文件",
                                AllowMultiple = true,
                                FileTypeFilter = new[]
                                {
                                    new FilePickerFileType("发票文件")
                                    {
                                        Patterns = new[] { "*.pdf", "*.jpg", "*.jpeg", "*.png" }
                                    }
                                }
                            });

                        if (pickerFiles != null && pickerFiles.Count > 0)
                        {
                            files = pickerFiles;
                        }
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"打开文件选择对话框失败: {ex.Message}";
                    return;
                }
            }

            if (files == null)
            {
                StatusMessage = "未选择任何文件";
                return;
            }

            IsProcessing = true;
            var fileList = files.ToList();
            StatusMessage = $"正在处理 {fileList.Count} 个文件...";

            try
            {
                foreach (var file in fileList)
                {
                    await ProcessSingleFileAsync(file);
                }

                StatusMessage = $"成功处理 {fileList.Count} 个文件";
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理文件失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ProcessSingleFileAsync(IStorageFile file)
        {
            try
            {
                // 读取文件
                await using var stream = await file.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                var fileName = file.Name;
                
                // 保存到临时目录
                var filePath = await _fileManager.SaveUploadedFileAsync(fileBytes, fileName);

                // 创建发票对象
                var invoice = new InvoiceInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Status = InvoiceStatus.Processing,
                    InvoiceDate = DateTime.Now
                };

                // 必须在 UI 线程更新集合
                UploadedInvoices.Add(invoice);
                StatusMessage = $"正在识别 {fileName}...";

                byte[] imageBytes;
                bool isPdf = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

                if (isPdf)
                {
                    try
                    {
                        StatusMessage = $"正在转换 PDF: {fileName}...";
                        imageBytes = await _pdfService.ConvertPdfToImageAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        invoice.Status = InvoiceStatus.Review;
                        invoice.RawOcrData = $"PDF 转换失败: {ex.Message}";
                        StatusMessage = $"PDF 转换失败: {fileName}";
                        return;
                    }
                }
                else
                {
                    imageBytes = fileBytes;
                }

                try
                {
                    // 调用OCR识别
                    var recognizedInvoice = await _ocrService.RecognizeInvoiceAsync(imageBytes, fileName);

                    // 更新发票信息
                    invoice.InvoiceDate = recognizedInvoice.InvoiceDate;
                    invoice.Amount = recognizedInvoice.Amount;
                    invoice.ItemName = recognizedInvoice.ItemName;
                    invoice.InvoiceNumber = recognizedInvoice.InvoiceNumber;
                    invoice.SellerName = recognizedInvoice.SellerName;
                    invoice.SellerTaxId = recognizedInvoice.SellerTaxId;
                    invoice.RawOcrData = recognizedInvoice.RawOcrData;
                    invoice.Status = InvoiceStatus.Review;

                    // 记录日志
                    _logger.LogUpload(fileName, invoice);
                }
                catch (Exception ex)
                {
                    invoice.Status = InvoiceStatus.Review;
                    StatusMessage = $"OCR识别失败: {ex.Message}，请手动编辑";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理 {file.Name} 失败: {ex.Message}";
            }
        }

        private async Task LoadPreviewImageAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    PreviewImageBytes = null;
                    return;
                }

                if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // 转换 PDF 第一页为图片进行预览
                    PreviewImageBytes = await _pdfService.ConvertPdfToImageAsync(filePath);
                }
                else
                {
                    PreviewImageBytes = await File.ReadAllBytesAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载预览失败: {ex.Message}";
                PreviewImageBytes = null;
            }
        }

        [RelayCommand]
        private async Task ArchiveSingleAsync(InvoiceInfo? invoice)
        {
            if (invoice == null) return;

            // 验证必填字段
            if (invoice.Amount <= 0)
            {
                StatusMessage = "请填写发票金额";
                return;
            }

            if (string.IsNullOrWhiteSpace(invoice.ItemName))
            {
                StatusMessage = "请填写项目名称";
                return;
            }

            IsProcessing = true;
            StatusMessage = $"正在归档 {invoice.FileName}...";

            try
            {
                await _fileManager.ArchiveInvoiceAsync(invoice);
                _logger.LogArchive(invoice.FileName);

                UploadedInvoices.Remove(invoice);
                StatusMessage = "归档成功";

                if (SelectedInvoice == invoice)
                {
                    SelectedInvoice = null;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"归档失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task ArchiveAllAsync()
        {
            if (UploadedInvoices.Count == 0)
            {
                StatusMessage = "没有可归档的发票";
                return;
            }

            // 检查是否所有发票都有必填信息
            var invalidInvoices = UploadedInvoices.Where(i => 
                i.Amount <= 0 || string.IsNullOrWhiteSpace(i.ItemName)).ToList();

            if (invalidInvoices.Any())
            {
                StatusMessage = $"有 {invalidInvoices.Count} 个发票信息不完整，请先完善";
                return;
            }

            IsProcessing = true;
            StatusMessage = "正在批量归档...";

            try
            {
                var invoicesToArchive = UploadedInvoices.ToList();
                int successCount = 0;

                foreach (var invoice in invoicesToArchive)
                {
                    try
                    {
                        await _fileManager.ArchiveInvoiceAsync(invoice);
                        _logger.LogArchive(invoice.FileName);
                        UploadedInvoices.Remove(invoice);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"归档 {invoice.FileName} 失败: {ex.Message}";
                    }
                }

                StatusMessage = $"成功归档 {successCount}/{invoicesToArchive.Count} 个发票";
                SelectedInvoice = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量归档失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void DeleteInvoice(InvoiceInfo? invoice)
        {
            if (invoice == null) return;

            try
            {
                _fileManager.DeleteTempFile(invoice.FilePath);
                UploadedInvoices.Remove(invoice);

                if (SelectedInvoice == invoice)
                {
                    SelectedInvoice = null;
                }

                StatusMessage = "已删除";
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private void SelectInvoice(InvoiceInfo? invoice)
        {
            SelectedInvoice = invoice;
        }

        [RelayCommand]
        private void SetUniformDate()
        {
            if (UploadedInvoices.Count == 0)
            {
                StatusMessage = "没有可设置日期的发票";
                return;
            }

            // 使用选择器中的日期
            var selectedDate = UniformUploadDate.DateTime;

            foreach (var invoice in UploadedInvoices)
            {
                invoice.InvoiceDate = selectedDate;
            }

            StatusMessage = $"已将 {UploadedInvoices.Count} 个发票的日期统一设置为: {selectedDate:yyyy-MM-dd}";
        }
    }
}
