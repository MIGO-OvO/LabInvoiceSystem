using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabInvoiceSystem.Models
{
    public enum InvoiceStatus
    {
        Pending,       // 待识别
        Processing,    // 识别中
        Review,        // 待审核
        Archived       // 已归档
    }

    public partial class InvoiceInfo : ObservableObject
    {
        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private DateTime _invoiceDate = DateTime.Now;

        [ObservableProperty]
        private decimal _amount;

        [ObservableProperty]
        private string _itemName = string.Empty;

        [ObservableProperty]
        private string _paymentMethod = "公务卡";

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private InvoiceStatus _status = InvoiceStatus.Pending;

        [ObservableProperty]
        private string _rawOcrData = string.Empty;
    }
}
