using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabInvoiceSystem.Models;
using LabInvoiceSystem.Services;

namespace LabInvoiceSystem.ViewModels
{
    public partial class StatisticsViewModel : ViewModelBase, INavigable
    {
        private readonly FileManagerService _fileManager;
        private readonly StatisticsService _statisticsService;
        private readonly HttpClient _httpClient;

        [ObservableProperty]
        private StatisticsData _statistics = new();

        [ObservableProperty]
        private ObservableCollection<HeatmapDayData> _heatmapData = new();

        [ObservableProperty]
        private string _statusMessage = "准备就绪";
        
        // KPI Properties
        [ObservableProperty]
        private decimal _totalAmount;
        
        [ObservableProperty]
        private int _totalCount;
        
        [ObservableProperty]
        private decimal _last30DaysAmount;

        [ObservableProperty]
        private string _apiStatusText = string.Empty;

        [ObservableProperty]
        private string _monthlyApiUsageText = string.Empty;

        [ObservableProperty]
        private bool _isApiConfigured;

        [ObservableProperty]
        private bool _isApiSettingsOpen;

        [ObservableProperty]
        private string _apiKeyInput = string.Empty;

        [ObservableProperty]
        private string _secretKeyInput = string.Empty;

        [ObservableProperty]
        private bool _isTestingConnection;

        [ObservableProperty]
        private string _testResultMessage = string.Empty;

        [ObservableProperty]
        private bool _isTestSuccess;

        public StatisticsViewModel()
        {
            _fileManager = new FileManagerService();
            _statisticsService = new StatisticsService();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task OnNavigatedTo()
        {
            LoadApiStatusFromSettings();
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            StatusMessage = "正在刷新统计数据...";

            try
            {
                // 加载归档数据
                var archives = _fileManager.GetArchivedInvoices();
                
                // 计算统计数据
                Statistics = _statisticsService.CalculateStatistics(archives);
                
                // Update KPI properties
                TotalAmount = Statistics.TotalAmount;
                TotalCount = Statistics.InvoiceCount;
                Last30DaysAmount = Statistics.Last30DaysAmount;

                // 生成热力图数据
                GenerateHeatmapData();

                StatusMessage = $"已加载 {archives.Count} 个发票的统计数据";
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
            }

            await Task.CompletedTask;
        }

        private void GenerateHeatmapData()
        {
            HeatmapData.Clear();

            // 生成过去365天的数据
            var today = DateTime.Now.Date;
            var startDate = today.AddDays(-364); // 包含今天共365天

            // 计算颜色级别的阈值
            var amounts = Statistics.DailyExpenses.Values.Where(v => v > 0).ToList();
            var maxAmount = amounts.Any() ? amounts.Max() : 0;
            
            // 使用分位数或简单的等分方法
            var threshold1 = maxAmount * 0.25m;
            var threshold2 = maxAmount * 0.5m;
            var threshold3 = maxAmount * 0.75m;

            for (int i = 0; i < 365; i++)
            {
                var date = startDate.AddDays(i);
                var amount = Statistics.DailyExpenses.ContainsKey(date) ? Statistics.DailyExpenses[date] : 0;
                
                // 确定颜色级别 (0-4)
                int level;
                string colorHex;
                
                if (amount == 0)
                {
                    level = 0;
                    colorHex = "#F1F5F9"; // Slate100
                }
                else if (amount < threshold1)
                {
                    level = 1;
                    colorHex = "#C7D2FE"; // Indigo200
                }
                else if (amount < threshold2)
                {
                    level = 2;
                    colorHex = "#818CF8"; // Indigo400
                }
                else if (amount < threshold3)
                {
                    level = 3;
                    colorHex = "#4F46E5"; // Indigo600
                }
                else
                {
                    level = 4;
                    colorHex = "#3730A3"; // Indigo800
                }

                HeatmapData.Add(new HeatmapDayData
                {
                    Date = date,
                    Amount = amount,
                    Level = level,
                    ColorHex = colorHex
                });
            }
        }

        private void LoadApiStatusFromSettings()
        {
            var settings = SettingsService.Instance.Settings;

            IsApiConfigured = !string.IsNullOrWhiteSpace(settings.BaiduApiKey) &&
                              !string.IsNullOrWhiteSpace(settings.BaiduSecretKey);

            ApiStatusText = IsApiConfigured ? "API 就绪" : "未配置";

            var currentMonth = DateTime.Now.ToString("yyyy-MM");
            var usage = settings.BaiduMonthlyUsage;

            if (!string.IsNullOrWhiteSpace(settings.BaiduUsageMonth) && settings.BaiduUsageMonth != currentMonth)
            {
                usage = 0;
            }

            var quota = settings.BaiduMonthlyQuota > 0 ? settings.BaiduMonthlyQuota : 1000;

            MonthlyApiUsageText = $"本月调用: {usage}/{quota}";
        }

        [RelayCommand]
        private void OpenApiSettings()
        {
            var settings = SettingsService.Instance.Settings;

            ApiKeyInput = settings.BaiduApiKey ?? string.Empty;
            SecretKeyInput = settings.BaiduSecretKey ?? string.Empty;
            TestResultMessage = string.Empty;
            IsTestSuccess = false;
            IsTestingConnection = false;

            IsApiSettingsOpen = true;
        }

        [RelayCommand]
        private void CloseApiSettings()
        {
            IsApiSettingsOpen = false;
        }

        [RelayCommand]
        private async Task TestApiAsync()
        {
            if (string.IsNullOrWhiteSpace(ApiKeyInput) || string.IsNullOrWhiteSpace(SecretKeyInput))
            {
                TestResultMessage = "请填写完整的 API Key 与 Secret Key";
                IsTestSuccess = false;
                return;
            }

            IsTestingConnection = true;
            TestResultMessage = string.Empty;
            IsTestSuccess = false;

            try
            {
                var url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={ApiKeyInput}&client_secret={SecretKeyInput}";

                var response = await _httpClient.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    TestResultMessage = $"连接失败: {body}";
                    IsTestSuccess = false;
                    return;
                }

                using var jsonDoc = JsonDocument.Parse(body);

                if (jsonDoc.RootElement.TryGetProperty("access_token", out _))
                {
                    TestResultMessage = "连接成功，API Key 有效";
                    IsTestSuccess = true;
                }
                else if (jsonDoc.RootElement.TryGetProperty("error_description", out var errorDesc))
                {
                    TestResultMessage = $"连接失败: {errorDesc.GetString()}";
                    IsTestSuccess = false;
                }
                else
                {
                    TestResultMessage = "连接结果未知，请检查配置或稍后重试";
                    IsTestSuccess = false;
                }
            }
            catch (Exception ex)
            {
                TestResultMessage = $"连接失败: {ex.Message}";
                IsTestSuccess = false;
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        [RelayCommand]
        private void SaveApiConfig()
        {
            var settings = SettingsService.Instance.Settings;

            settings.BaiduApiKey = ApiKeyInput?.Trim() ?? string.Empty;
            settings.BaiduSecretKey = SecretKeyInput?.Trim() ?? string.Empty;

            SettingsService.Instance.SaveSettings();

            LoadApiStatusFromSettings();

            IsApiSettingsOpen = false;
        }
    }
}
