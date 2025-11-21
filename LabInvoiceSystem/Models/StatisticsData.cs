using System.Collections.Generic;

namespace LabInvoiceSystem.Models
{
    public class StatisticsData
    {
        public decimal TotalAmount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal AverageAmount => InvoiceCount > 0 ? TotalAmount / InvoiceCount : 0;
        
        public Dictionary<string, decimal> MonthlyExpenses { get; set; } = new();
        public Dictionary<string, int> PaymentMethodStats { get; set; } = new();
    }
}
