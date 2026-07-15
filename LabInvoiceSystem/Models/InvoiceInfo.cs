using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabInvoiceSystem.Models
{
    public enum InvoiceStatus
    {
        Pending,       // 待识别
        ConvertingPdf,
        OcrProcessing,
        Review,        // 待审核
        Failed,
        Archived       // 已归档
    }

    public partial class InvoiceInfo : ObservableObject
    {
        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private DateTime _invoiceDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _entryDate = DateTime.Today;

        [ObservableProperty]
        private decimal _amount;

        [ObservableProperty]
        private string _itemName = string.Empty;

        [ObservableProperty]
        private string _ocrItemName = string.Empty;

        [ObservableProperty]
        private string _paymentMethod = "公务卡";

        [ObservableProperty]
        private string _invoiceNumber = string.Empty;

        [ObservableProperty]
        private string _sellerName = string.Empty;

        [ObservableProperty]
        private string _sellerTaxId = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private InvoiceStatus _status = InvoiceStatus.Pending;

        [ObservableProperty]
        private string _rawOcrData = string.Empty;

        [ObservableProperty]
        private string _processingMessage = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _canRetryOcr;

        [ObservableProperty]
        private bool _showValidationErrors;

        [ObservableProperty]
        private string _duplicateWarning = string.Empty;

        public bool HasProcessingMessage => !string.IsNullOrWhiteSpace(ProcessingMessage);

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasAmountError => ShowValidationErrors && Amount <= 0;

        public string AmountError => HasAmountError ? "金额必须大于 0" : string.Empty;

        public bool HasItemNameError => ShowValidationErrors && string.IsNullOrWhiteSpace(ItemName);

        public string ItemNameError => HasItemNameError ? "请填写项目名称" : string.Empty;

        public bool HasDuplicateWarning => !string.IsNullOrWhiteSpace(DuplicateWarning);

        public void ShowValidation() => ShowValidationErrors = true;

        public void HideValidation() => ShowValidationErrors = false;

        partial void OnProcessingMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasProcessingMessage));
        }

        partial void OnErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasErrorMessage));
        }

        partial void OnShowValidationErrorsChanged(bool value) => NotifyValidationChanged();

        partial void OnAmountChanged(decimal value)
        {
            NotifyValidationChanged();
            DuplicateWarning = string.Empty;
        }

        partial void OnItemNameChanged(string value)
        {
            NotifyValidationChanged();
            DuplicateWarning = string.Empty;
        }

        partial void OnInvoiceDateChanged(DateTime value) => DuplicateWarning = string.Empty;

        partial void OnInvoiceNumberChanged(string value) => DuplicateWarning = string.Empty;

        partial void OnSellerNameChanged(string value) => DuplicateWarning = string.Empty;

        partial void OnSellerTaxIdChanged(string value) => DuplicateWarning = string.Empty;

        partial void OnDuplicateWarningChanged(string value) => OnPropertyChanged(nameof(HasDuplicateWarning));

        private void NotifyValidationChanged()
        {
            OnPropertyChanged(nameof(HasAmountError));
            OnPropertyChanged(nameof(AmountError));
            OnPropertyChanged(nameof(HasItemNameError));
            OnPropertyChanged(nameof(ItemNameError));
        }
    }
}
