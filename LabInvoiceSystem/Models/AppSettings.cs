namespace LabInvoiceSystem.Models
{
    public class AppSettings
    {
        // 百度 OCR API 配置
        public string BaiduAppId { get; set; } = "7242830";
        public string BaiduApiKey { get; set; } = "8jhD0bIfMEJpXR8xrarXKOpm";
        public string BaiduSecretKey { get; set; } = "8lqqwekTEpfBFCmCZljEl8FWumynC2lq";
        public int BaiduMonthlyUsage { get; set; } = 0;
        public int BaiduMonthlyQuota { get; set; } = 1000;
        public string BaiduUsageMonth { get; set; } = string.Empty;
        
        // 文件路径配置
        public string ArchiveDirectory { get; set; } = "archive_data";
        public string TempUploadDirectory { get; set; } = "temp_uploads";
        public string ExportDirectory { get; set; } = "export_data";
        
        // 主题设置
        public string ThemeMode { get; set; } = "Dark";  // Dark 或 Light
    }
}
