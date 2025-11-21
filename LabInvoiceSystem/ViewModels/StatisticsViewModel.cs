using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabInvoiceSystem.Models;
using LabInvoiceSystem.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing;

namespace LabInvoiceSystem.ViewModels
{
    public partial class StatisticsViewModel : ViewModelBase, INavigable
    {
        private readonly FileManagerService _fileManager;
        private readonly StatisticsService _statisticsService;
        private readonly LoggerService _logger;

        [ObservableProperty]
        private StatisticsData _statistics = new();

        [ObservableProperty]
        private ISeries[] _monthlyTrendSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _paymentMethodSeries = Array.Empty<ISeries>();
        
        [ObservableProperty]
        private Axis[] _xAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private ObservableCollection<string> _paymentLegends = new();

        [ObservableProperty]
        private ObservableCollection<LogEntry> _historyLogs = new();

        [ObservableProperty]
        private string _statusMessage = "准备就绪";
        
        // KPI Properties
        [ObservableProperty]
        private decimal _totalAmount;
        
        [ObservableProperty]
        private int _totalCount;
        
        [ObservableProperty]
        private decimal _averageAmount;

        public StatisticsViewModel()
        {
            _fileManager = new FileManagerService();
            _statisticsService = new StatisticsService();
            _logger = new LoggerService();
        }

        public async Task OnNavigatedTo()
        {
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
                AverageAmount = Statistics.AverageAmount;

                // 生成月度支出图表
                GenerateMonthlyChart();

                // 生成支付方式饼图
                GeneratePaymentPieChart();

                // 加载日志
                var logs = _logger.GetLogs();
                HistoryLogs.Clear();
                foreach (var log in logs.Take(50)) // 只显示最新 50 条
                {
                    HistoryLogs.Add(log);
                }

                StatusMessage = $"已加载 {archives.Count} 个发票的统计数据";
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
            }

            await Task.CompletedTask;
        }

        private void GenerateMonthlyChart()
        {
            var orderedStats = Statistics.MonthlyExpenses.OrderBy(kv => kv.Key).ToList();
            var values = orderedStats.Select(kv => (double)kv.Value).ToArray();
            var labels = orderedStats.Select(kv => kv.Key).ToArray();

            MonthlyTrendSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = "月度支出",
                    Values = values,
                    Fill = new SolidColorPaint(SKColors.DeepSkyBlue),
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 }
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = 0,
                    SeparatorsPaint = new SolidColorPaint(new SKColor(200, 200, 200)),
                    SeparatorsAtCenter = false,
                    TicksPaint = new SolidColorPaint(new SKColor(35, 35, 35)),
                    TicksAtCenter = true
                }
            };
        }

        private void GeneratePaymentPieChart()
        {
            if (Statistics.PaymentMethodStats.Count == 0)
            {
                PaymentMethodSeries = Array.Empty<ISeries>();
                return;
            }

            var labelPaint = new SolidColorPaint(SKColors.White)
            {
                SKTypeface = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Normal)
            };

            PaymentMethodSeries = Statistics.PaymentMethodStats
                .Select(kv => new PieSeries<int>
                {
                    Name = kv.Key,
                    Values = new[] { kv.Value },
                    DataLabelsFormatter = point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue}张",
                    DataLabelsPaint = labelPaint,
                    DataLabelsSize = 14,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
                })
                .ToArray();

            PaymentLegends.Clear();
            foreach (var kv in Statistics.PaymentMethodStats)
            {
                PaymentLegends.Add($"{kv.Key}: {kv.Value} 张");
            }
        }

        [RelayCommand]
        private void ClearHistory()
        {
            _logger.ClearLogs();
            HistoryLogs.Clear();
            StatusMessage = "日志已清空";
        }
    }
}
