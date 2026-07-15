using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PDFtoImage;
using SkiaSharp;

namespace LabInvoiceSystem.Services
{
    public class PdfService
    {
        // 默认基准DPI，用于无法获取屏幕缩放比例时
        private const int DefaultBaseDpi = 150;

        /// <summary>
        /// 将PDF转换为图片，使用默认DPI（150）
        /// </summary>
        /// <param name="pdfPath">PDF文件路径</param>
        /// <returns>图片字节数组</returns>
        public async Task<byte[]> ConvertPdfToImageAsync(string pdfPath)
        {
            return await ConvertPdfToImageAsync(pdfPath, 1.0);
        }

        /// <summary>
        /// 将PDF转换为图片，根据屏幕DPI缩放比例动态计算渲染DPI
        /// </summary>
        /// <param name="pdfPath">PDF文件路径</param>
        /// <param name="dpiScale">屏幕DPI缩放比例（1.0 = 100%, 1.25 = 125%等）</param>
        /// <returns>图片字节数组</returns>
        public async Task<byte[]> ConvertPdfToImageAsync(string pdfPath, double dpiScale)
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"PDF 文件不存在: {pdfPath}");
            }

            var targetDpi = ScreenHelper.CalculatePdfRenderDpi(dpiScale);
            var cachePath = GetPreviewCachePath(pdfPath, targetDpi);

            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath);
            }

            return await Task.Run(async () =>
            {
                var outputPath = cachePath + ".tmp";
                try
                {
                    var fileInfo = new FileInfo(pdfPath);
                    if (fileInfo.Length == 0)
                    {
                        throw new Exception($"PDF 文件为空（0 字节）: {pdfPath}");
                    }

                    // 检查文件头是否为 PDF 格式 (%PDF-)
                    byte[] header = new byte[5];
                    using (var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
                    {
                        await fs.ReadAsync(header, 0, 5);
                    }
                    
                    string headerStr = System.Text.Encoding.ASCII.GetString(header);
                    if (!headerStr.StartsWith("%PDF-"))
                    {
                        throw new Exception($"文件不是有效的 PDF 格式（文件头: {headerStr}），请确认上传的是真实的 PDF 文件");
                    }

                    // 读取 PDF 内容并转换为 Base64（PDFtoImage 需要 Base64 字符串作为输入）
                    var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
                    var base64Pdf = Convert.ToBase64String(pdfBytes);

                    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() ||
                        OperatingSystem.IsMacOS() || OperatingSystem.IsAndroidVersionAtLeast(31))
                    {
                        Conversion.SavePng(outputPath, base64Pdf, 0, null, new RenderOptions { Dpi = targetDpi });
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("当前系统不支持 PDF 预览转换");
                    }

                    if (!File.Exists(outputPath))
                    {
                        throw new Exception("PDF 转换未生成输出图片文件");
                    }

                    File.Move(outputPath, cachePath, true);
                    return await File.ReadAllBytesAsync(cachePath);
                }
                catch (FileNotFoundException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var fileSize = File.Exists(pdfPath) ? new FileInfo(pdfPath).Length : 0;
                    throw new Exception($"PDF 转换失败 (文件: {Path.GetFileName(pdfPath)}, 大小: {fileSize} 字节): {ex.Message}", ex);
                }
                finally
                {
                    if (File.Exists(outputPath))
                    {
                        try { File.Delete(outputPath); } catch { }
                    }
                }
            });
        }

        private static string GetPreviewCachePath(string pdfPath, int targetDpi)
        {
            var fileInfo = new FileInfo(pdfPath);
            var cacheDir = Path.Combine(SettingsService.Instance.Settings.TempUploadDirectory, "previews");
            Directory.CreateDirectory(cacheDir);

            var key = $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}|{targetDpi}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            return Path.Combine(cacheDir, $"{hash}.png");
        }
    }
}
