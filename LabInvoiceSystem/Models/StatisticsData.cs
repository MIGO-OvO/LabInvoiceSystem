using System;
using System.Collections.Generic;

namespace LabInvoiceSystem.Models
{
    public class StatisticsData
    {
        public decimal TotalAmount { get; set; }
        public int InvoiceCount { get; set; }
        
        public decimal Last30DaysAmount { get; set; }
        public Dictionary<DateTime, decimal> DailyExpenses { get; set; } = new();
    }
}
