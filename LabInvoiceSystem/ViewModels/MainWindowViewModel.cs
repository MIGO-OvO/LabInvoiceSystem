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

        [ObservableProperty]
        private bool _isExitConfirmationOpen;

        [ObservableProperty]
        private string _shellMessage = string.Empty;

        public bool HasShellMessage => !string.IsNullOrWhiteSpace(ShellMessage);

        partial void OnShellMessageChanged(string value) => OnPropertyChanged(nameof(HasShellMessage));

        // Track if user manually closed the pane to prevent auto-reopening
        private bool _userManuallyClosed = false;

        private InvoiceImportViewModel? _importViewModel;
        private InvoiceExportViewModel? _exportViewModel;
        private StatisticsViewModel? _statisticsViewModel;

        public MainWindowViewModel()
        {
            // Default view
            _importViewModel = new InvoiceImportViewModel();
            _currentView = _importViewModel;
        }

        public bool HasPendingInvoices => _importViewModel?.UploadedInvoices.Count > 0;

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
            // Track user's manual action
            _userManuallyClosed = !IsPaneOpen;
        }

        /// <summary>
        /// Set pane open state from window resize without triggering user action tracking.
        /// Only closes the pane automatically, never opens it (respects user's manual close).
        /// </summary>
        public void SetPaneOpenWithoutUserAction(bool isOpen)
        {
            // Only auto-close, never auto-open if user manually closed
            if (!isOpen)
            {
                IsPaneOpen = false;
            }
            else if (!_userManuallyClosed)
            {
                IsPaneOpen = true;
            }
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            var app = Application.Current;
            if (app is null) return;

            var currentTheme = app.RequestedThemeVariant;
            var newTheme = currentTheme == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
            var settings = SettingsService.Instance.Settings;
            var previousMode = settings.ThemeMode;
            app.RequestedThemeVariant = newTheme;
            settings.ThemeMode = newTheme == ThemeVariant.Dark ? "Dark" : "Light";
            if (!SettingsService.Instance.SaveSettings())
            {
                settings.ThemeMode = previousMode;
                app.RequestedThemeVariant = currentTheme;
                ShellMessage = "主题偏好保存失败，已恢复原主题";
                return;
            }

            ShellMessage = string.Empty;
        }
    }
}
