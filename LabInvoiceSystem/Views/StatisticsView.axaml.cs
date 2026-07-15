using Avalonia.Controls;
using Avalonia;

namespace LabInvoiceSystem.Views
{
    public partial class StatisticsView : UserControl
    {
        public StatisticsView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var compact = e.NewSize.Width < 760;
            KpiGrid.ColumnDefinitions = compact
                ? new ColumnDefinitions("*")
                : new ColumnDefinitions("*,16,*,16,*");
            KpiGrid.RowDefinitions = compact
                ? new RowDefinitions("Auto,12,Auto,12,Auto")
                : new RowDefinitions("Auto");

            Grid.SetColumn(TotalAmountCard, 0);
            Grid.SetRow(TotalAmountCard, 0);
            Grid.SetColumn(InvoiceCountCard, compact ? 0 : 2);
            Grid.SetRow(InvoiceCountCard, compact ? 2 : 0);
            Grid.SetColumn(Last30DaysCard, compact ? 0 : 4);
            Grid.SetRow(Last30DaysCard, compact ? 4 : 0);
        }
    }
}
