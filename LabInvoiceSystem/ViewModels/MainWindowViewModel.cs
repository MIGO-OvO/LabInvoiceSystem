using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabInvoiceSystem.Services;
using Avalonia.Styling;
using Avalonia;

namespace LabInvoiceSystem.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private string _currentViewName = "Import";

        [ObservableProperty]
        private bool _isPaneOpen = true;

        private InvoiceImportViewModel? _importViewModel;
        private InvoiceExportViewModel? _exportViewModel;
        private StatisticsViewModel? _statisticsViewModel;

        public MainWindowViewModel()
        {
            // Default view
            _importViewModel = new InvoiceImportViewModel();
            _currentView = _importViewModel;
        }

        [RelayCommand]
        private void Navigate(string viewName)
        {
            if (CurrentViewName == viewName) return;

            CurrentViewName = viewName;
            switch (viewName)
            {
                case "Import":
                    _importViewModel ??= new InvoiceImportViewModel();
                    CurrentView = _importViewModel;
                    break;
                case "Export":
                    _exportViewModel ??= new InvoiceExportViewModel();
                    CurrentView = _exportViewModel;
                    break;
                case "Statistics":
                    _statisticsViewModel ??= new StatisticsViewModel();
                    CurrentView = _statisticsViewModel;
                    break;
            }

            if (CurrentView is INavigable navigable)
            {
                _ = navigable.OnNavigatedTo();
            }
        }

        [RelayCommand]
        private void TriggerPane()
        {
            IsPaneOpen = !IsPaneOpen;
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            var app = Application.Current;
            if (app is null) return;

            var currentTheme = app.RequestedThemeVariant;
            var newTheme = currentTheme == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
            app.RequestedThemeVariant = newTheme;

            // Save setting
            var settings = SettingsService.Instance.Settings;
            settings.ThemeMode = newTheme == ThemeVariant.Dark ? "Dark" : "Light";
            SettingsService.Instance.SaveSettings();
        }
    }
}
