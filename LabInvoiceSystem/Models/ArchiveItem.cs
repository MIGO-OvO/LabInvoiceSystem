using CommunityToolkit.Mvvm.ComponentModel;

namespace LabInvoiceSystem.Models
{
    public partial class ArchiveItem : ObservableObject
    {
        [ObservableProperty]
        private string _yearMonth = string.Empty;

        [ObservableProperty]
        private string _date = string.Empty; // YYYY-MM-DD

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private InvoiceInfo _invoiceInfo = new();
    }
}
