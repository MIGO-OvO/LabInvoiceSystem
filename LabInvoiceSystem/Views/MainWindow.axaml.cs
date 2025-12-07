using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LabInvoiceSystem.Services;
using LabInvoiceSystem.ViewModels;

namespace LabInvoiceSystem.Views
{
    public partial class MainWindow : Window
    {
        // 导航面板自动折叠的窗口宽度阈值
        private const double SmallWindowThreshold = 800;
        
        // Viewbox设计尺寸的宽高比（4:3）
        private const double DesignAspectRatio = 4.0 / 3.0;
        
        public MainWindow()
        {
            InitializeComponent();
            ConfigureWindowSize();
            
            // 订阅窗口尺寸变化以实现响应式导航和Viewbox调整
            this.GetObservable(BoundsProperty).Subscribe(new BoundsObserver(this));
        }
        
        /// <summary>
        /// 用于处理窗口边界变化的观察者类
        /// </summary>
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

        /// <summary>
        /// 根据屏幕分辨率和DPI缩放配置窗口大小
        /// 确保窗口尺寸永远不超出屏幕有效分辨率
        /// </summary>
        private void ConfigureWindowSize()
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var physicalWidth = screen.Bounds.Width;
                var physicalHeight = screen.Bounds.Height;
                var scaling = screen.Scaling;

                // 获取最优窗口尺寸（已考虑DPI缩放）
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
                
                // 如果是小屏幕或窗口较小，自动折叠导航面板
                var isSmallScreen = ScreenHelper.IsSmallScreen(physicalWidth, physicalHeight, scaling);
                if ((width < SmallWindowThreshold || isSmallScreen) && DataContext is MainWindowViewModel vm)
                {
                    vm.IsPaneOpen = false;
                }
            }
            else
            {
                // 无法获取屏幕信息时使用安全的默认尺寸
                Width = ScreenHelper.RecommendedMinWidth;
                Height = ScreenHelper.RecommendedMinHeight;
                MinWidth = ScreenHelper.AbsoluteMinWidth;
                MinHeight = ScreenHelper.AbsoluteMinHeight;
            }
        }

        /// <summary>
        /// 处理窗口边界变化，实现响应式导航行为和动态Viewbox调整
        /// </summary>
        private void OnBoundsChanged(Rect bounds)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            
            // 当窗口变小时自动折叠导航面板
            if (bounds.Width < SmallWindowThreshold && vm.IsPaneOpen)
            {
                vm.SetPaneOpenWithoutUserAction(false);
            }
            
            // 动态调整Viewbox的设计尺寸以匹配窗口宽高比
            AdjustViewboxSize(bounds);
        }
        
        /// <summary>
        /// 动态调整Viewbox的设计尺寸以匹配窗口宽高比
        /// 这样可以在保持UI元素比例缩放的同时，避免过多的空白边距
        /// </summary>
        private void AdjustViewboxSize(Rect bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            
            // 计算窗口的宽高比
            var windowAspectRatio = bounds.Width / bounds.Height;
            
            // 根据窗口宽高比动态调整Viewbox的设计尺寸
            // 保持固定的基准高度，调整宽度以匹配窗口比例
            const double baseHeight = 800.0;
            var adjustedWidth = baseHeight * windowAspectRatio;
            
            // 限制宽度范围，避免极端情况
            adjustedWidth = Math.Clamp(adjustedWidth, 1000.0, 1600.0);
            
            // 更新Viewbox内部Border的尺寸
            if (this.FindControl<Border>("ViewboxContent") is Border border)
            {
                border.Width = adjustedWidth;
                border.Height = baseHeight;
            }
        }

        private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }
    }
}