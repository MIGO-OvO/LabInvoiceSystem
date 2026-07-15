using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LabInvoiceSystem.Services;
using LabInvoiceSystem.ViewModels;

namespace LabInvoiceSystem.Views
{
    public partial class MainWindow : Window
    {
        private const double SmallWindowThreshold = 800;
        private bool _allowClose;
        
        public MainWindow()
        {
            InitializeComponent();
            ConfigureWindowSize();
            this.GetObservable(BoundsProperty).Subscribe(new BoundsObserver(this));
            Closing += OnClosing;
        }

        private class BoundsObserver : IObserver<Rect>
        {
            private readonly MainWindow _window;
            
            public BoundsObserver(MainWindow window)
            {
                _window = window;
            }
            
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            
            public void OnNext(Rect value)
            {
                _window.OnBoundsChanged(value);
            }
        }

        private void ConfigureWindowSize()
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var physicalWidth = screen.Bounds.Width;
                var physicalHeight = screen.Bounds.Height;
                var scaling = screen.Scaling;

                var (width, height) = ScreenHelper.GetOptimalWindowSize(
                    physicalWidth, physicalHeight, scaling);
                var (minWidth, minHeight) = ScreenHelper.GetMinWindowSize(
                    physicalWidth, physicalHeight, scaling);
                var (maxWidth, maxHeight) = ScreenHelper.GetMaxWindowSize(
                    physicalWidth, physicalHeight, scaling);

                Width = width;
                Height = height;
                MinWidth = minWidth;
                MinHeight = minHeight;
                MaxWidth = maxWidth;
                MaxHeight = maxHeight;
                
                var isSmallScreen = ScreenHelper.IsSmallScreen(physicalWidth, physicalHeight, scaling);
                if ((width < SmallWindowThreshold || isSmallScreen) && DataContext is MainWindowViewModel vm)
                {
                    vm.IsPaneOpen = false;
                }
            }
            else
            {
                Width = ScreenHelper.RecommendedMinWidth;
                Height = ScreenHelper.RecommendedMinHeight;
                MinWidth = ScreenHelper.AbsoluteMinWidth;
                MinHeight = ScreenHelper.AbsoluteMinHeight;
            }
        }

        private void OnBoundsChanged(Rect bounds)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            if (bounds.Width < SmallWindowThreshold && vm.IsPaneOpen)
            {
                vm.SetPaneOpenWithoutUserAction(false);
            }
        }

        private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_allowClose || DataContext is not MainWindowViewModel vm || !vm.HasPendingInvoices)
            {
                return;
            }

            e.Cancel = true;
            vm.IsExitConfirmationOpen = true;
            Dispatcher.UIThread.Post(() => this.FindControl<Button>("ContinueWorkButton")?.Focus());
        }

        private void OnContinueWorkClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsExitConfirmationOpen = false;
            }
        }

        private void OnDiscardAndExitClick(object? sender, RoutedEventArgs e)
        {
            _allowClose = true;
            Close();
        }
    }
}
