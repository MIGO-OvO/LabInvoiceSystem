using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class FileManagerService
    {
        private readonly string _archiveDir;
        private readonly string _tempUploadDir;

        public FileManagerService()
        {
            var settings = SettingsService.Instance.Settings;
            _archiveDir = settings.ArchiveDirectory;
            _tempUploadDir = settings.TempUploadDirectory;

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
                        var fileName = Path.GetFileName(file);
                        var invoiceInfo = ParseFileNameToInvoice(fileName, file);

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
