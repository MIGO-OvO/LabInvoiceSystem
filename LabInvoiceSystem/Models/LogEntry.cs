using CommunityToolkit.Mvvm.ComponentModel;

namespace LabInvoiceSystem.Models
{
    public partial class LogEntry : ObservableObject
    {
        [ObservableProperty]
        private System.DateTime _timestamp = System.DateTime.Now;

        [ObservableProperty]
        private string _action = string.Empty;

        [ObservableProperty]
        private string _details = string.Empty;
    }
}
