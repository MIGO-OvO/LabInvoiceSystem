using System;
using System.IO;
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
            // 使用ScreenHelper计算目标渲染DPI
            var targetDpi = ScreenHelper.CalculatePdfRenderDpi(dpiScale);

            return await Task.Run(async () =>
            {
                var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                try
                {
                    if (!File.Exists(pdfPath))
                    {
                        throw new FileNotFoundException($"PDF 文件不存在: {pdfPath}");
                    }

                    // 验证文件不为空
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

                    // PDFtoImage.Conversion.SavePng expects (outputPath, base64Pdf, pageIndex, password, options)
                    // 使用动态计算的DPI进行渲染，以获得高清预览效果
                    Conversion.SavePng(outputPath, base64Pdf, 0, null, new RenderOptions { Dpi = targetDpi });

                    if (!File.Exists(outputPath))
                    {
                        throw new Exception("PDF 转换未生成输出图片文件");
                    }

                    return await File.ReadAllBytesAsync(outputPath);
                }
                catch (FileNotFoundException fnfEx)
                {
                    throw fnfEx;
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
    }
}
