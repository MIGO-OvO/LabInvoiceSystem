using System.Collections.Generic;
using System.Linq;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class StatisticsService
    {
        public StatisticsData CalculateStatistics(List<ArchiveItem> archives)
        {
            var stats = new StatisticsData();

            if (archives == null || archives.Count == 0)
            {
                return stats;
            }

            // 计算总支出和发票数量
            stats.TotalAmount = archives.Sum(a => a.InvoiceInfo.Amount);
            stats.InvoiceCount = archives.Count;

            // 计算月度支出
            stats.MonthlyExpenses = GetMonthlyExpenses(archives);

            // 计算支付方式统计
            stats.PaymentMethodStats = GetPaymentMethodDistribution(archives);

            return stats;
        }

        public Dictionary<string, decimal> GetMonthlyExpenses(List<ArchiveItem> archives)
        {
            return archives
                .GroupBy(a => a.YearMonth)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => a.InvoiceInfo.Amount)
                );
        }

        public Dictionary<string, int> GetPaymentMethodDistribution(List<ArchiveItem> archives)
        {
            return archives
                .GroupBy(a => a.InvoiceInfo.PaymentMethod)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );
        }
    }
}
