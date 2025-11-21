using System;
using System.IO;
using System.Threading.Tasks;
using PDFtoImage;
using SkiaSharp;

namespace LabInvoiceSystem.Services
{
    public class PdfService
    {
        public async Task<byte[]> ConvertPdfToImageAsync(string pdfPath)
        {
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
                    // It converts the first page by default.
                    Conversion.SavePng(outputPath, base64Pdf, 0, null, new RenderOptions { Dpi = 300 });

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
