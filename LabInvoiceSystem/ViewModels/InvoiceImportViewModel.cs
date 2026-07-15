using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabInvoiceSystem.Models;
using LabInvoiceSystem.Services;
using Avalonia.Media.Imaging;

namespace LabInvoiceSystem.ViewModels
{
    public partial class InvoiceImportViewModel : ViewModelBase, INavigable
    {
        private readonly OcrService _ocrService;
        private readonly FileManagerService _fileManager;
        private readonly LoggerService _logger;
        private readonly PdfService _pdfService;
        private CancellationTokenSource? _uploadCancellationTokenSource;
        private bool _manualEntryForSession;

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

        [ObservableProperty]
        private int _batchTotalCount;

        [ObservableProperty]
        private int _batchProcessedCount;

        [ObservableProperty]
        private int _batchSuccessCount;

        [ObservableProperty]
        private int _batchFailedCount;

        [ObservableProperty]
        private bool _isCloudOcrNoticeVisible;

        public InvoiceImportViewModel()
        {
            _ocrService = new OcrService();
            _fileManager = new FileManagerService();
            _logger = new LoggerService();
            _pdfService = new PdfService();
            RefreshCloudOcrNotice();
            RestorePendingUploads();
        }

        public Task OnNavigatedTo()
        {
            RefreshCloudOcrNotice();
            return Task.CompletedTask;
        }

        private void RefreshCloudOcrNotice()
        {
            var settings = SettingsService.Instance.Settings;
            IsCloudOcrNoticeVisible = IsOcrConfigured() && !settings.CloudOcrConsentAccepted && !_manualEntryForSession;
        }

        private void RestorePendingUploads()
        {
            foreach (var path in _fileManager.GetPendingUploadFiles())
            {
                UploadedInvoices.Add(new InvoiceInfo
                {
                    FileName = Path.GetFileName(path),
                    FilePath = path,
                    Status = InvoiceStatus.Review,
                    InvoiceDate = File.GetLastWriteTime(path),
                    EntryDate = File.GetCreationTime(path).Date,
                    ProcessingMessage = "上次会话未完成，请重新核对"
                });
            }

            if (UploadedInvoices.Count > 0)
            {
                SelectedInvoice = UploadedInvoices[0];
                StatusMessage = $"已恢复 {UploadedInvoices.Count} 个未归档文件";
            }
        }

        [RelayCommand]
        private void AcceptCloudOcrNotice()
        {
            var settings = SettingsService.Instance.Settings;
            settings.CloudOcrConsentAccepted = true;
            if (!SettingsService.Instance.SaveSettings())
            {
                settings.CloudOcrConsentAccepted = false;
                StatusMessage = "无法保存云端 OCR 选择，请检查用户目录写入权限";
                return;
            }

            _manualEntryForSession = false;
            IsCloudOcrNoticeVisible = false;
            StatusMessage = "已启用百度云 OCR，发票图像将在识别时上传";
        }

        [RelayCommand]
        private void UseManualEntry()
        {
            _manualEntryForSession = true;
            IsCloudOcrNoticeVisible = false;
            StatusMessage = "本次会话仅手工录入，不会上传发票图像";
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
            _uploadCancellationTokenSource?.Cancel();
            _uploadCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _uploadCancellationTokenSource.Token;
            BatchTotalCount = fileList.Count;
            BatchProcessedCount = 0;
            BatchSuccessCount = 0;
            BatchFailedCount = 0;
            StatusMessage = $"正在处理 {fileList.Count} 个文件...";

            try
            {
                using var semaphore = new SemaphoreSlim(3);
                var tasks = fileList.Select(file => ProcessFileWithLimitAsync(file, semaphore, cancellationToken)).ToList();
                await Task.WhenAll(tasks);

                if (cancellationToken.IsCancellationRequested)
                {
                    StatusMessage = $"已取消，完成 {BatchProcessedCount}/{BatchTotalCount} 个文件";
                }
                else
                {
                    StatusMessage = $"已完成 {BatchProcessedCount}/{BatchTotalCount} 个文件，成功 {BatchSuccessCount}，失败 {BatchFailedCount}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理文件失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                _uploadCancellationTokenSource?.Dispose();
                _uploadCancellationTokenSource = null;
            }
        }

        [RelayCommand]
        private void CancelProcessing()
        {
            if (_uploadCancellationTokenSource == null)
            {
                return;
            }

            _uploadCancellationTokenSource.Cancel();
            StatusMessage = "正在取消未开始的处理...";
        }

        private async Task ProcessFileWithLimitAsync(IStorageFile file, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            var entered = false;
            try
            {
                await semaphore.WaitAsync(cancellationToken);
                entered = true;
                await ProcessSingleFileAsync(file, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (entered)
                {
                    semaphore.Release();
                }
            }
        }

        private async Task ProcessSingleFileAsync(IStorageFile file, CancellationToken cancellationToken)
        {
            InvoiceInfo? invoice = null;
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                var fileBytes = memoryStream.ToArray();

                var fileName = file.Name;
                
                cancellationToken.ThrowIfCancellationRequested();
                var filePath = await _fileManager.SaveUploadedFileAsync(fileBytes, fileName);

                invoice = new InvoiceInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Status = InvoiceStatus.Pending,
                    ProcessingMessage = "等待处理",
                    InvoiceDate = DateTime.Now,
                    EntryDate = DateTime.Today
                };

                await RunOnUiThreadAsync(() =>
                {
                    UploadedInvoices.Add(invoice);
                    SelectedInvoice ??= invoice;
                    StatusMessage = $"已加入队列: {fileName}";
                });

                if (!CanUseCloudOcr())
                {
                    var unavailableMessage = GetCloudOcrUnavailableMessage();
                    invoice.Status = InvoiceStatus.Review;
                    invoice.ProcessingMessage = string.Empty;
                    invoice.ErrorMessage = unavailableMessage;
                    invoice.RawOcrData = unavailableMessage;
                    await MarkProcessedAsync(false);
                    return;
                }

                await RecognizeExistingInvoiceAsync(invoice, fileBytes, cancellationToken);
                await MarkProcessedAsync(invoice.Status != InvoiceStatus.Failed);
            }
            catch (OperationCanceledException)
            {
                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.Pending;
                    invoice.ProcessingMessage = "已取消";
                }
            }
            catch (Exception ex)
            {
                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.Failed;
                    invoice.ProcessingMessage = string.Empty;
                    invoice.ErrorMessage = ex.Message;
                    invoice.CanRetryOcr = true;
                    await MarkProcessedAsync(false);
                }
                else
                {
                    await RunOnUiThreadAsync(() => StatusMessage = $"处理 {file.Name} 失败: {ex.Message}");
                    await MarkProcessedAsync(false);
                }
            }
        }

        [RelayCommand]
        private async Task RetryOcrAsync(InvoiceInfo? invoice)
        {
            if (invoice == null || IsProcessing)
            {
                return;
            }

            if (!File.Exists(invoice.FilePath))
            {
                StatusMessage = "源文件不存在，无法重试";
                return;
            }

            if (!CanUseCloudOcr())
            {
                invoice.ErrorMessage = GetCloudOcrUnavailableMessage();
                StatusMessage = invoice.ErrorMessage;
                RefreshCloudOcrNotice();
                return;
            }

            IsProcessing = true;
            try
            {
                var fileBytes = invoice.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? Array.Empty<byte>()
                    : await File.ReadAllBytesAsync(invoice.FilePath);
                await RecognizeExistingInvoiceAsync(invoice, fileBytes, CancellationToken.None);
                StatusMessage = invoice.Status == InvoiceStatus.Failed ? $"重试失败: {invoice.FileName}" : $"重试成功: {invoice.FileName}";
            }
            catch (Exception ex)
            {
                invoice.Status = InvoiceStatus.Failed;
                invoice.ProcessingMessage = string.Empty;
                invoice.ErrorMessage = ex.Message;
                invoice.CanRetryOcr = true;
                StatusMessage = $"重试失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task RecognizeExistingInvoiceAsync(InvoiceInfo invoice, byte[] fileBytes, CancellationToken cancellationToken)
        {
            invoice.ErrorMessage = string.Empty;
            invoice.CanRetryOcr = false;

            byte[] imageBytes;
            var isPdf = invoice.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            if (isPdf)
            {
                invoice.Status = InvoiceStatus.ConvertingPdf;
                invoice.ProcessingMessage = "正在转换 PDF";
                imageBytes = await _pdfService.ConvertPdfToImageAsync(invoice.FilePath);
            }
            else
            {
                imageBytes = fileBytes.Length > 0 ? fileBytes : await File.ReadAllBytesAsync(invoice.FilePath, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            invoice.Status = InvoiceStatus.OcrProcessing;
            invoice.ProcessingMessage = "正在 OCR 识别";

            try
            {
                var recognizedInvoice = await _ocrService.RecognizeInvoiceAsync(imageBytes, invoice.FileName);
                invoice.InvoiceDate = recognizedInvoice.InvoiceDate;
                invoice.Amount = recognizedInvoice.Amount;
                invoice.InvoiceNumber = recognizedInvoice.InvoiceNumber;
                invoice.SellerName = recognizedInvoice.SellerName;
                invoice.SellerTaxId = recognizedInvoice.SellerTaxId;
                invoice.OcrItemName = recognizedInvoice.ItemName;
                invoice.ItemName = SettingsService.Instance.ApplyItemNameCorrection(
                    invoice.SellerTaxId, invoice.SellerName, invoice.OcrItemName);
                invoice.RawOcrData = recognizedInvoice.RawOcrData;
                invoice.Status = InvoiceStatus.Review;
                invoice.ProcessingMessage = "识别完成，请核对";
                invoice.ErrorMessage = string.Empty;
                invoice.CanRetryOcr = false;

                _logger.LogUpload(invoice.FileName, invoice);
            }
            catch (Exception ex)
            {
                invoice.Status = InvoiceStatus.Failed;
                invoice.ProcessingMessage = string.Empty;
                invoice.ErrorMessage = $"OCR 识别失败: {ex.Message}";
                invoice.CanRetryOcr = true;
            }
        }

        private async Task MarkProcessedAsync(bool success)
        {
            await RunOnUiThreadAsync(() =>
            {
                BatchProcessedCount++;
                if (success)
                {
                    BatchSuccessCount++;
                }
                else
                {
                    BatchFailedCount++;
                }

                StatusMessage = $"已处理 {BatchProcessedCount}/{BatchTotalCount}，成功 {BatchSuccessCount}，失败 {BatchFailedCount}";
            });
        }

        private static async Task RunOnUiThreadAsync(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(action);
        }

        private static bool IsOcrConfigured()
        {
            var settings = SettingsService.Instance.Settings;
            return !string.IsNullOrWhiteSpace(settings.BaiduApiKey) &&
                   !string.IsNullOrWhiteSpace(settings.BaiduSecretKey);
        }

        private bool CanUseCloudOcr()
        {
            var settings = SettingsService.Instance.Settings;
            return IsOcrConfigured() && settings.CloudOcrConsentAccepted && !_manualEntryForSession;
        }

        private string GetCloudOcrUnavailableMessage()
        {
            if (!IsOcrConfigured())
            {
                return "OCR API 未配置，请手工录入或前往仪表盘配置百度 OCR API。";
            }

            return _manualEntryForSession
                ? "本次会话已选择手工录入，发票图像未上传。"
                : "尚未同意使用百度云 OCR，发票图像未上传。";
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
                    // 获取当前屏幕的DPI缩放比例
                    var dpiScale = GetCurrentScreenDpiScale();
                    
                    // 转换 PDF 第一页为图片进行预览，使用DPI感知的渲染
                    PreviewImageBytes = await _pdfService.ConvertPdfToImageAsync(filePath, dpiScale);
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

        /// <summary>
        /// 获取当前屏幕的DPI缩放比例
        /// </summary>
        /// <returns>DPI缩放比例（1.0 = 100%, 1.25 = 125%等）</returns>
        private double GetCurrentScreenDpiScale()
        {
            try
            {
                var app = Application.Current;
                if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow is not null)
                {
                    var screen = desktop.MainWindow.Screens.Primary;
                    if (screen != null)
                    {
                        return screen.Scaling;
                    }
                }
            }
            catch
            {
                // 如果获取失败，返回默认值
            }
            
            return 1.0;
        }

        [RelayCommand]
        private async Task ArchiveSingleAsync(InvoiceInfo? invoice)
        {
            await TryArchiveSingleAsync(invoice, false);
        }

        [RelayCommand]
        private async Task ArchiveDuplicateAsync(InvoiceInfo? invoice)
        {
            await TryArchiveSingleAsync(invoice, true);
        }

        [RelayCommand]
        private void CancelDuplicate(InvoiceInfo? invoice)
        {
            if (invoice == null) return;
            invoice.DuplicateWarning = string.Empty;
            StatusMessage = "已取消归档，请继续核对";
        }

        private async Task TryArchiveSingleAsync(InvoiceInfo? invoice, bool allowDuplicate)
        {
            if (invoice == null) return;

            invoice.ShowValidation();
            if (invoice.HasAmountError || invoice.HasItemNameError)
            {
                SelectedInvoice = invoice;
                StatusMessage = invoice.HasAmountError ? "金额必须大于 0" : "请填写项目名称";
                return;
            }

            IsProcessing = true;
            StatusMessage = $"正在归档 {invoice.FileName}...";

            try
            {
                if (!allowDuplicate)
                {
                    var duplicate = await _fileManager.FindDuplicateInvoiceAsync(invoice);
                    if (duplicate != null)
                    {
                        invoice.DuplicateWarning = $"疑似与已归档的“{duplicate.FileName}”重复。请核对后再决定。";
                        SelectedInvoice = invoice;
                        StatusMessage = "发现疑似重复发票，已暂停归档";
                        return;
                    }
                }

                var index = UploadedInvoices.IndexOf(invoice);
                await _fileManager.ArchiveInvoiceAsync(invoice);
                SettingsService.Instance.RememberItemNameCorrection(
                    invoice.SellerTaxId, invoice.SellerName, invoice.OcrItemName, invoice.ItemName);
                _logger.LogArchive(invoice.FileName);

                UploadedInvoices.Remove(invoice);
                StatusMessage = "归档成功";

                if (SelectedInvoice == invoice)
                {
                    SelectedInvoice = UploadedInvoices.Count == 0
                        ? null
                        : UploadedInvoices[Math.Min(index, UploadedInvoices.Count - 1)];
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

            foreach (var invoice in UploadedInvoices)
            {
                invoice.ShowValidation();
            }

            var invalidInvoices = UploadedInvoices.Where(i => i.HasAmountError || i.HasItemNameError).ToList();

            if (invalidInvoices.Any())
            {
                SelectedInvoice = invalidInvoices[0];
                StatusMessage = $"{invalidInvoices[0].FileName} 信息不完整，另有 {invalidInvoices.Count - 1} 个待处理";
                return;
            }

            IsProcessing = true;
            StatusMessage = "正在批量归档...";

            try
            {
                var invoicesToArchive = UploadedInvoices.ToList();
                int successCount = 0;

                for (var index = 1; index < invoicesToArchive.Count; index++)
                {
                    var invoice = invoicesToArchive[index];
                    var duplicateInBatch = await _fileManager.FindDuplicateInCandidatesAsync(
                        invoice, invoicesToArchive.Take(index));
                    if (duplicateInBatch != null)
                    {
                        invoice.DuplicateWarning = $"疑似与本批次的“{duplicateInBatch.FileName}”重复。请单独确认。";
                        SelectedInvoice = invoice;
                        StatusMessage = "批量归档已暂停：本批次中存在疑似重复发票";
                        return;
                    }
                }

                foreach (var invoice in invoicesToArchive)
                {
                    var duplicate = await _fileManager.FindDuplicateInvoiceAsync(invoice);
                    if (duplicate != null)
                    {
                        invoice.DuplicateWarning = $"疑似与已归档的“{duplicate.FileName}”重复。请单独确认。";
                        SelectedInvoice = invoice;
                        StatusMessage = "批量归档已暂停：发现疑似重复发票";
                        return;
                    }
                }

                foreach (var invoice in invoicesToArchive)
                {
                    try
                    {
                        await _fileManager.ArchiveInvoiceAsync(invoice);
                        SettingsService.Instance.RememberItemNameCorrection(
                            invoice.SellerTaxId, invoice.SellerName, invoice.OcrItemName, invoice.ItemName);
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
                var index = UploadedInvoices.IndexOf(invoice);
                _fileManager.DeleteTempFile(invoice.FilePath);
                UploadedInvoices.Remove(invoice);

                if (SelectedInvoice == invoice)
                {
                    SelectedInvoice = UploadedInvoices.Count == 0
                        ? null
                        : UploadedInvoices[Math.Min(index, UploadedInvoices.Count - 1)];
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

            StatusMessage = $"已将 {UploadedInvoices.Count} 张发票的购买日期统一设置为: {selectedDate:yyyy-MM-dd}";
        }
    }
}
