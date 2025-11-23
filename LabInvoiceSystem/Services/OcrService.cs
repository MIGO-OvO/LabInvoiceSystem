using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class OcrService
    {
        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private DateTime _tokenExpiration;

        public OcrService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // 检查 token 是否有效
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiration)
            {
                return _accessToken;
            }

            var settings = SettingsService.Instance.Settings;
            var url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials" +
                      $"&client_id={settings.BaiduApiKey}&client_secret={settings.BaiduSecretKey}";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var jsonDoc = JsonDocument.Parse(response);
                
                _accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
                var expiresIn = jsonDoc.RootElement.GetProperty("expires_in").GetInt32();
                _tokenExpiration = DateTime.Now.AddSeconds(expiresIn - 60); // 提前 60 秒过期

                return _accessToken ?? throw new Exception("获取 Access Token 失败");
            }
            catch (Exception ex)
            {
                throw new Exception($"获取百度 API Access Token 失败: {ex.Message}");
            }
        }

        public async Task<InvoiceInfo> RecognizeInvoiceAsync(byte[] imageBytes, string fileName)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new Exception("图片数据为空");
            }

            var accessToken = await GetAccessTokenAsync();
            var url = $"https://aip.baidubce.com/rest/2.0/ocr/v1/vat_invoice?access_token={accessToken}";

            // 转换为 Base64
            var base64Image = Convert.ToBase64String(imageBytes);
            
            // 使用 FormUrlEncodedContent 正确编码参数
            var formData = new Dictionary<string, string>
            {
                { "image", base64Image }
            };
            var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OCR 识别失败: {responseBody}");
            }

            var invoice = ParseOcrResult(responseBody, fileName);

            IncrementMonthlyUsage();

            return invoice;
        }

        private InvoiceInfo ParseOcrResult(string jsonResponse, string fileName)
        {
            var invoice = new InvoiceInfo
            {
                FileName = fileName,
                Status = InvoiceStatus.Review
            };

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (!jsonDoc.RootElement.TryGetProperty("words_result", out var wordsResult) ||
                    wordsResult.ValueKind != JsonValueKind.Object)
                {
                    throw new Exception("OCR 返回缺少 words_result 对象");
                }

                // 提取开票日期
                if (TryGetString(wordsResult, "InvoiceDate", out var dateRaw) &&
                    !string.IsNullOrWhiteSpace(dateRaw))
                {
                    var parsedDate = ParseInvoiceDate(dateRaw);
                    if (parsedDate.HasValue)
                    {
                        invoice.InvoiceDate = parsedDate.Value;
                    }
                }

                // 提取价税合计（优先 AmountInFiguers，失败再尝试 TotalAmount）
                var amountFields = new[] { "AmountInFiguers", "TotalAmount" };
                string? amountRaw = null;
                foreach (var field in amountFields)
                {
                    if (TryGetString(wordsResult, field, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        amountRaw = value;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(amountRaw))
                {
                    var normalized = NormalizeDecimalString(amountRaw);
                    if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    {
                        invoice.Amount = amount;
                    }
                }

                // 提取货物或应税劳务名称及规格型号
                var normalizedNames = ExtractCommodityList(wordsResult, "CommodityName");
                var normalizedSpecs = ExtractCommodityList(wordsResult, "CommodityType");

                var mergedItems = MergeNameAndSpec(normalizedNames, normalizedSpecs);
                if (mergedItems.Count > 0)
                {
                    invoice.ItemName = string.Join(", ", mergedItems);
                }

                // 提取发票号码
                if (TryGetString(wordsResult, "InvoiceNum", out var invoiceNum) &&
                    !string.IsNullOrWhiteSpace(invoiceNum))
                {
                    invoice.InvoiceNumber = invoiceNum;
                }

                // 提取销售方名称
                if (TryGetString(wordsResult, "SellerName", out var sellerName) &&
                    !string.IsNullOrWhiteSpace(sellerName))
                {
                    invoice.SellerName = sellerName;
                }

                // 提取销售方税号
                if (TryGetString(wordsResult, "SellerRegisterNum", out var sellerTaxId) &&
                    !string.IsNullOrWhiteSpace(sellerTaxId))
                {
                    invoice.SellerTaxId = sellerTaxId;
                }

                invoice.RawOcrData = jsonResponse;
            }
            catch (Exception ex)
            {
                throw new Exception($"解析OCR结果失败: {ex.Message}\n原始数据: {jsonResponse}", ex);
            }

            return invoice;
        }

        private List<string> MergeNameAndSpec(List<string> names, List<string> specs)
        {
            var result = new List<string>();
            var maxCount = Math.Max(names.Count, specs.Count);

            for (int i = 0; i < maxCount; i++)
            {
                var spec = i < specs.Count ? specs[i] : string.Empty;
                var name = i < names.Count ? names[i] : string.Empty;
                var final = !string.IsNullOrWhiteSpace(spec) ? spec : name;

                if (!string.IsNullOrWhiteSpace(final))
                {
                    result.Add(final);
                }
            }

            return result;
        }

        private List<string> ExtractCommodityList(JsonElement wordsResult, string propertyName)
        {
            if (!wordsResult.TryGetProperty(propertyName, out var element))
            {
                return new List<string>();
            }

            return EnumerateWords(element)
                .Select(CleanItemName)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .ToList();
        }

        private string CleanItemName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // 去除特殊符号
            var cleaned = input.Replace("*", "")
                              .Replace("#", "")
                              .Replace("&", "")
                              .Replace("-", "") // Remove hyphen to prevent parsing errors
                              .Replace("_", "")
                              .Trim();

            return cleaned;
        }

        private static bool TryGetString(JsonElement parent, string propertyName, out string? value)
        {
            value = null;
            if (!parent.TryGetProperty(propertyName, out var element))
            {
                return false;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    value = element.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                case JsonValueKind.Object:
                    if (element.TryGetProperty("word", out var wordElement))
                    {
                        value = wordElement.GetString();
                        return !string.IsNullOrWhiteSpace(value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        var word = ExtractWord(item);
                        if (!string.IsNullOrWhiteSpace(word))
                        {
                            value = word;
                            return true;
                        }
                    }
                    break;
            }

            return false;
        }

        private static IEnumerable<string> EnumerateWords(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var word = ExtractWord(item);
                    if (!string.IsNullOrWhiteSpace(word))
                    {
                        yield return word!;
                    }
                }
            }
            else
            {
                var word = ExtractWord(element);
                if (!string.IsNullOrWhiteSpace(word))
                {
                    yield return word!;
                }
            }
        }

        private static string? ExtractWord(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object when element.TryGetProperty("word", out var wordElement) => wordElement.GetString(),
                JsonValueKind.String => element.GetString(),
                _ => null
            };
        }

        private static DateTime? ParseInvoiceDate(string raw)
        {
            var candidates = new[]
            {
                "yyyyMMdd",
                "yyyy年MM月dd日",
                "yyyy-MM-dd",
                "yyyy/MM/dd"
            };

            foreach (var format in candidates)
            {
                if (DateTime.TryParseExact(raw, format, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }

            if (DateTime.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string NormalizeDecimalString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = raw
                .Replace("¥", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            return normalized;
        }

        private void IncrementMonthlyUsage()
        {
            try
            {
                var settings = SettingsService.Instance.Settings;
                var currentMonth = DateTime.Now.ToString("yyyy-MM");

                if (string.IsNullOrWhiteSpace(settings.BaiduUsageMonth) || settings.BaiduUsageMonth != currentMonth)
                {
                    settings.BaiduUsageMonth = currentMonth;
                    settings.BaiduMonthlyUsage = 0;
                }

                settings.BaiduMonthlyUsage++;
                SettingsService.Instance.SaveSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新百度 API 月调用计数失败: {ex.Message}");
            }
        }
    }
}
