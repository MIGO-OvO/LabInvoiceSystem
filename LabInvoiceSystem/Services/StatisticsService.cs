using System;
using System.Collections.Generic;
using System.Linq;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class StatisticsService
    {
#if DEBUG
        static StatisticsService()
        {
            var today = DateTime.Now.Date;
            var samples = new List<ArchiveItem>
            {
                new() { InvoiceInfo = new InvoiceInfo { InvoiceDate = today, Amount = 1 } },
                new() { InvoiceInfo = new InvoiceInfo { InvoiceDate = today.AddDays(-29), Amount = 2 } },
                new() { InvoiceInfo = new InvoiceInfo { InvoiceDate = today.AddDays(-30), Amount = 4 } }
            };
            if (new StatisticsService().CalculateLast30DaysAmount(samples) != 3)
            {
                throw new InvalidOperationException("Last-30-days statistics self-check failed.");
            }
        }
#endif

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

            // 计算每日支出
            stats.DailyExpenses = GetDailyExpenses(archives);

            // 计算近30天报账金额
            stats.Last30DaysAmount = CalculateLast30DaysAmount(archives);

            return stats;
        }

        public Dictionary<DateTime, decimal> GetDailyExpenses(List<ArchiveItem> archives)
        {
            return archives
                .GroupBy(a => a.InvoiceInfo.InvoiceDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => a.InvoiceInfo.Amount)
                );
        }

        public decimal CalculateLast30DaysAmount(List<ArchiveItem> archives)
        {
            // 包含今天在内的连续 30 个自然日。
            var thirtyDaysAgo = DateTime.Now.Date.AddDays(-29);
            return archives
                .Where(a => a.InvoiceInfo.InvoiceDate.Date >= thirtyDaysAgo)
                .Sum(a => a.InvoiceInfo.Amount);
        }
    }
}
