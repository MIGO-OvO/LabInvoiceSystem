using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LabInvoiceSystem.Models;
using MiniExcelLibs;

namespace LabInvoiceSystem.Services
{
    public class FileManagerService
    {
        private readonly string _archiveDir;
        private readonly string _tempUploadDir;
        private readonly string _exportDir;

        public FileManagerService()
        {
            var settings = SettingsService.Instance.Settings;
            _archiveDir = settings.ArchiveDirectory;
            _tempUploadDir = settings.TempUploadDirectory;
            _exportDir = settings.ExportDirectory;

            SettingsService.Instance.EnsureDirectoriesExist();
        }

        public async Task<string> SaveUploadedFileAsync(byte[] fileBytes, string fileName)
        {
            try
            {
                var filePath = Path.Combine(_tempUploadDir, fileName);
                
                // 如果文件已存在,添加序号后缀
                filePath = GetUniqueFilePath(filePath);

                await File.WriteAllBytesAsync(filePath, fileBytes);
                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"保存文件失败: {ex.Message}");
            }
        }

        public void CleanupTempUploadDirectory()
        {
            try
            {
                if (!Directory.Exists(_tempUploadDir))
                {
                    return;
                }

                foreach (var file in Directory.GetFiles(_tempUploadDir))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"清理临时上传文件失败: {ex.Message}");
                    }
                }

                foreach (var directory in Directory.GetDirectories(_tempUploadDir))
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"清理临时上传子目录失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理临时上传目录失败: {ex.Message}");
            }
        }

        public async Task ArchiveInvoiceAsync(InvoiceInfo invoice)
        {
            try
            {
                if (string.IsNullOrEmpty(invoice.FilePath) || !File.Exists(invoice.FilePath))
                {
                    throw new Exception("源文件不存在");
                }

                // 生成归档文件名: YYYYMMDD-项目名称-支付方式-金额元.pdf
                var dateStr = invoice.InvoiceDate.ToString("yyyyMMdd");
                var extension = Path.GetExtension(invoice.FilePath);
                
                // Sanitize parts to ensure they don't contain separators
                var safeItemName = invoice.ItemName?.Replace("-", "_") ?? "未命名";
                var safePaymentMethod = invoice.PaymentMethod?.Replace("-", "_") ?? "未分类";
                
                var newFileName = $"{dateStr}-{safeItemName}-{safePaymentMethod}-{invoice.Amount}元{extension}";
                
                // 清理文件名中的非法字符
                newFileName = CleanFileName(newFileName);

                // 目标目录: archive_data/YYYY-MM/
                var yearMonth = invoice.InvoiceDate.ToString("yyyy-MM");
                var targetDir = Path.Combine(_archiveDir, yearMonth);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var targetPath = Path.Combine(targetDir, newFileName);
                targetPath = GetUniqueFilePath(targetPath);

                // 移动文件
                File.Move(invoice.FilePath, targetPath);

                // 更新状态
                invoice.Status = InvoiceStatus.Archived;
                invoice.FilePath = targetPath;

                // 写入元数据 JSON
                try
                {
                    var metadata = new InvoiceMetadata
                    {
                        InvoiceDate = invoice.InvoiceDate,
                        Amount = invoice.Amount,
                        ItemName = invoice.ItemName ?? string.Empty,
                        PaymentMethod = invoice.PaymentMethod ?? string.Empty,
                        InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                        SellerName = invoice.SellerName ?? string.Empty,
                        SellerTaxId = invoice.SellerTaxId ?? string.Empty
                    };

                    var metadataPath = Path.ChangeExtension(targetPath, ".json");
                    var json = JsonSerializer.Serialize(metadata);
                    File.WriteAllText(metadataPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存发票元数据失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"归档文件失败: {ex.Message}");
            }
        }

        public List<ArchiveItem> GetArchivedInvoices()
        {
            var archives = new List<ArchiveItem>();

            try
            {
                if (!Directory.Exists(_archiveDir))
                {
                    return archives;
                }

                var monthDirs = Directory.GetDirectories(_archiveDir).OrderByDescending(d => d);

                foreach (var monthDir in monthDirs)
                {
                    var yearMonth = Path.GetFileName(monthDir);
                    var files = Directory.GetFiles(monthDir).OrderBy(f => f);

                    foreach (var file in files)
                    {
                        // 跳过元数据文件
                        if (string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(file);
                        InvoiceInfo invoiceInfo;

                        var metadataPath = Path.ChangeExtension(file, ".json");
                        if (File.Exists(metadataPath))
                        {
                            try
                            {
                                var json = File.ReadAllText(metadataPath);
                                var metadata = JsonSerializer.Deserialize<InvoiceMetadata>(json);

                                if (metadata != null)
                                {
                                    invoiceInfo = new InvoiceInfo
                                    {
                                        FileName = fileName,
                                        FilePath = file,
                                        Status = InvoiceStatus.Archived,
                                        InvoiceDate = metadata.InvoiceDate,
                                        Amount = metadata.Amount,
                                        ItemName = metadata.ItemName ?? string.Empty,
                                        PaymentMethod = metadata.PaymentMethod ?? string.Empty,
                                        InvoiceNumber = metadata.InvoiceNumber ?? string.Empty,
                                        SellerName = metadata.SellerName ?? string.Empty,
                                        SellerTaxId = metadata.SellerTaxId ?? string.Empty
                                    };
                                }
                                else
                                {
                                    invoiceInfo = ParseFileNameToInvoice(fileName, file);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"读取发票元数据失败: {ex.Message}");
                                invoiceInfo = ParseFileNameToInvoice(fileName, file);
                            }
                        }
                        else
                        {
                            invoiceInfo = ParseFileNameToInvoice(fileName, file);
                        }

                        archives.Add(new ArchiveItem
                        {
                            YearMonth = yearMonth,
                            Date = invoiceInfo.InvoiceDate.ToString("yyyy-MM-dd"),
                            FileName = fileName,
                            FilePath = file,
                            InvoiceInfo = invoiceInfo
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取归档列表失败: {ex.Message}");
            }

            return archives;
        }

        public async Task<string> ExportInvoicesToZipAsync(List<string> filePaths, string outputFileName)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "LabInvoiceExport");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var zipPath = Path.Combine(tempDir, outputFileName);

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var filePath in filePaths)
                    {
                        if (File.Exists(filePath))
                        {
                            var entryName = Path.GetFileName(filePath);
                            zipArchive.CreateEntryFromFile(filePath, entryName);
                        }
                    }
                }

                return zipPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"导出 ZIP 失败: {ex.Message}");
            }
        }

        public async Task<string> ExportInvoicesToZipWithExcelAsync(List<ArchiveItem> archives, string outputFileName, string excelFileName)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "LabInvoiceExport");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var targetDir = !string.IsNullOrWhiteSpace(_exportDir) ? _exportDir : tempDir;
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var zipPath = Path.Combine(targetDir, outputFileName);
                var excelPath = Path.Combine(tempDir, excelFileName);

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                if (File.Exists(excelPath))
                {
                    File.Delete(excelPath);
                }

                // 准备 Excel 数据
                var rows = archives.Select(a =>
                {
                    var info = a.InvoiceInfo ?? new InvoiceInfo();
                    return new Dictionary<string, object?>
                    {
                        ["日期"] = info.InvoiceDate,
                        ["金额"] = info.Amount,
                        ["项目名称"] = info.ItemName ?? string.Empty,
                        ["支付方式"] = info.PaymentMethod ?? string.Empty,
                        ["发票号码"] = info.InvoiceNumber ?? string.Empty,
                        ["销售方名称"] = info.SellerName ?? string.Empty,
                        ["销售方税号"] = info.SellerTaxId ?? string.Empty
                    };
                }).ToList();

                if (rows.Count == 0)
                {
                    // 仍然创建一个只有表头的空表，避免后续缺文件
                    var header = new List<Dictionary<string, object?>>
                    {
                        new()
                        {
                            ["日期"] = null,
                            ["金额"] = null,
                            ["项目名称"] = null,
                            ["支付方式"] = null,
                            ["发票号码"] = null,
                            ["销售方名称"] = null,
                            ["销售方税号"] = null
                        }
                    };
                    MiniExcel.SaveAs(excelPath, header);
                }
                else
                {
                    MiniExcel.SaveAs(excelPath, rows);
                }

                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    // 添加发票原文件
                    foreach (var item in archives)
                    {
                        if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
                        {
                            var entryName = Path.GetFileName(item.FilePath);
                            zipArchive.CreateEntryFromFile(item.FilePath, entryName);
                        }
                    }

                    // 添加 Excel 明细
                    if (File.Exists(excelPath))
                    {
                        zipArchive.CreateEntryFromFile(excelPath, excelFileName);
                    }
                }

                // 清理临时 Excel 文件
                try
                {
                    if (File.Exists(excelPath))
                    {
                        File.Delete(excelPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"清理临时 Excel 文件失败: {ex.Message}");
                }

                await Task.CompletedTask;
                return zipPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"导出 ZIP 和 Excel 失败: {ex.Message}");
            }
        }

        public void DeleteTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除临时文件失败: {ex.Message}");
            }
        }

        public async Task DeleteArchivedFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    DeleteMetadataIfExists(filePath);
                    TryDeleteParentDirectoryIfEmpty(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"删除归档文件失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        public async Task DeleteArchivedFilesAsync(List<string> filePaths)
        {
            int successCount = 0;
            var errors = new List<string>();

            foreach (var filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        DeleteMetadataIfExists(filePath);
                        TryDeleteParentDirectoryIfEmpty(filePath);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            if (errors.Any())
            {
                throw new Exception($"删除了 {successCount}/{filePaths.Count} 个文件。错误: {string.Join(", ", errors)}");
            }

            await Task.CompletedTask;
        }

        private void DeleteMetadataIfExists(string filePath)
        {
            try
            {
                var metadataPath = Path.ChangeExtension(filePath, ".json");
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除发票元数据失败: {ex.Message}");
            }
        }

        private void TryDeleteParentDirectoryIfEmpty(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                // 只处理归档目录下的子目录，避免误删其他路径
                if (!directory.StartsWith(_archiveDir, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var entries = Directory.GetFileSystemEntries(directory);
                if (entries.Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除空归档目录失败: {ex.Message}");
            }
        }

        private class InvoiceMetadata
        {
            public DateTime InvoiceDate { get; set; }
            public decimal Amount { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
            public string InvoiceNumber { get; set; } = string.Empty;
            public string SellerName { get; set; } = string.Empty;
            public string SellerTaxId { get; set; } = string.Empty;
        }

        private string CleanFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            int counter = 1;
            string newPath;

            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        private InvoiceInfo ParseFileNameToInvoice(string fileName, string filePath)
        {
            var invoice = new InvoiceInfo
            {
                FileName = fileName,
                FilePath = filePath,
                Status = InvoiceStatus.Archived
            };

            try
            {
                // 文件名格式: YYYYMMDD-项目名称-支付方式-金额元.pdf
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var parts = nameWithoutExt.Split('-');

                if (parts.Length >= 4)
                {
                    // 解析日期
                    if (DateTime.TryParseExact(parts[0], "yyyyMMdd", null, 
                        System.Globalization.DateTimeStyles.None, out var date))
                    {
                        invoice.InvoiceDate = date;
                    }

                    // 金额 (Always the last part)
                    var amountPart = parts.Last();
                    var amountStr = amountPart.Replace("元", "").Replace(Path.GetExtension(fileName), "");
                    if (decimal.TryParse(amountStr, out var amount))
                    {
                        invoice.Amount = amount;
                    }

                    // 支付方式 (Second to last)
                    if (parts.Length > 2)
                    {
                        invoice.PaymentMethod = parts[parts.Length - 2];
                    }

                    // 项目名称 (Everything in between)
                    if (parts.Length > 3)
                    {
                        // Join middle parts just in case, though we try to prevent extra hyphens
                        invoice.ItemName = string.Join("-", parts.Skip(1).Take(parts.Length - 3));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析文件名失败: {ex.Message}");
            }

            return invoice;
        }
    }
}
