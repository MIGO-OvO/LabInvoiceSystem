using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabInvoiceSystem.Models
{
    public partial class DateGroup : ObservableObject
    {
        private ObservableCollection<ArchiveItem> _invoices = new();

        public DateGroup()
        {
            _invoices.CollectionChanged += OnInvoicesChanged;
        }

        public string Date { get; set; } = string.Empty; // YYYY-MM-DD

        public ObservableCollection<DateGroup> PurchaseDateGroups { get; set; } = new();

        [ObservableProperty]
        private bool _isExpanded = true;

        public ObservableCollection<ArchiveItem> Invoices
        {
            get => _invoices;
            set
            {
                _invoices.CollectionChanged -= OnInvoicesChanged;
                foreach (var item in _invoices)
                    item.PropertyChanged -= OnInvoicePropertyChanged;

                _invoices = value;
                _invoices.CollectionChanged += OnInvoicesChanged;
                foreach (var item in _invoices)
                    item.PropertyChanged += OnInvoicePropertyChanged;
            }
        }
        
        public int TotalCount => Invoices.Count;
        public decimal TotalAmount => Invoices.Sum(i => i.InvoiceInfo?.Amount ?? 0);
        public int SelectedCount => Invoices.Count(i => i.IsSelected);

        public bool? IsAllSelected
        {
            get => SelectedCount == 0 ? false : SelectedCount == TotalCount ? true : null;
            set
            {
                if (!value.HasValue) return;
                foreach (var item in Invoices)
                    item.IsSelected = value.Value;
            }
        }

        private void OnInvoicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (ArchiveItem item in e.OldItems)
                    item.PropertyChanged -= OnInvoicePropertyChanged;
            if (e.NewItems != null)
                foreach (ArchiveItem item in e.NewItems)
                    item.PropertyChanged += OnInvoicePropertyChanged;

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalAmount));
            NotifySelectionChanged();
        }

        private void OnInvoicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ArchiveItem.IsSelected))
                NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(IsAllSelected));
        }
    }
}
