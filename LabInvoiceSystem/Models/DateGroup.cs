using System.Collections.ObjectModel;
using System.Linq;

namespace LabInvoiceSystem.Models
{
    public class DateGroup
    {
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public ObservableCollection<ArchiveItem> Invoices { get; set; } = new();
        
        public int TotalCount => Invoices.Count;
        public decimal TotalAmount => Invoices.Sum(i => i.InvoiceInfo?.Amount ?? 0);
    }
}
