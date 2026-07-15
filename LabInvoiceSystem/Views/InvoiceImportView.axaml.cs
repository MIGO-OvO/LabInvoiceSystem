using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LabInvoiceSystem.ViewModels;

namespace LabInvoiceSystem.Views
{
    public partial class InvoiceImportView : UserControl
    {
        // 缩放相关常量
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomStep = 0.1;
        
        // 当前缩放比例
        private double _currentZoom = 1.0;
        
        // 拖动相关
        private bool _isDragging;
        private Point _lastDragPoint;
        private Point _currentTranslate = new(0, 0);
        
        // 控件引用
        private Image? _previewImage;
        private Border? _previewContainer;
        
        public InvoiceImportView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;

            // 订阅DataContext变化以重置缩放
            DataContextChanged += OnDataContextChanged;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var mainGrid = this.FindControl<Grid>("MainSplitGrid");
            var leftPane = this.FindControl<Grid>("LeftPane");
            var rightPane = this.FindControl<Grid>("RightPane");
            var rightEmpty = this.FindControl<Grid>("RightEmpty");
            if (mainGrid == null || leftPane == null || rightPane == null || rightEmpty == null)
            {
                return;
            }

            mainGrid.ColumnDefinitions.Clear();
            mainGrid.RowDefinitions.Clear();

            if (e.NewSize.Width < 720)
            {
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                mainGrid.RowDefinitions.Add(new RowDefinition(new GridLength(180)));
                mainGrid.RowDefinitions.Add(new RowDefinition(new GridLength(12)));
                mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                Grid.SetColumn(leftPane, 0);
                Grid.SetRow(leftPane, 0);
                Grid.SetColumn(rightPane, 0);
                Grid.SetRow(rightPane, 2);
                Grid.SetColumn(rightEmpty, 0);
                Grid.SetRow(rightEmpty, 2);
                return;
            }

            mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(16)));
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(leftPane, 0);
            Grid.SetRow(leftPane, 0);
            Grid.SetColumn(rightPane, 2);
            Grid.SetRow(rightPane, 0);
            Grid.SetColumn(rightEmpty, 2);
            Grid.SetRow(rightEmpty, 0);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            // 获取控件引用
            _previewImage = this.FindControl<Image>("PreviewImage");
            _previewContainer = this.FindControl<Border>("PreviewContainer");
            
            // 订阅ViewModel的属性变化以在切换发票时重置缩放
            if (DataContext is InvoiceImportViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // 重置缩放状态
            ResetZoom();
            
            // 重新订阅属性变化
            if (DataContext is InvoiceImportViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当选中的发票变化时重置缩放
            if (e.PropertyName == nameof(InvoiceImportViewModel.SelectedInvoice) ||
                e.PropertyName == nameof(InvoiceImportViewModel.PreviewImageBytes))
            {
                ResetZoom();
            }
        }

        /// <summary>
        /// 重置缩放和平移状态
        /// </summary>
        private void ResetZoom()
        {
            _currentZoom = 1.0;
            _currentTranslate = new Point(0, 0);
            _isDragging = false;
            ApplyTransform();
        }

        /// <summary>
        /// 应用变换到图片
        /// </summary>
        private void ApplyTransform()
        {
            if (_previewImage == null) return;
            
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(_currentZoom, _currentZoom));
            transformGroup.Children.Add(new TranslateTransform(_currentTranslate.X, _currentTranslate.Y));
            _previewImage.RenderTransform = transformGroup;
        }

        /// <summary>
        /// 处理鼠标滚轮缩放
        /// </summary>
        private void OnPreviewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_previewImage == null || _previewContainer == null) return;
            
            // 获取鼠标相对于容器的位置
            var mousePos = e.GetPosition(_previewContainer);
            
            // 计算新的缩放比例
            var delta = e.Delta.Y > 0 ? ZoomStep : -ZoomStep;
            var newZoom = Math.Clamp(_currentZoom + delta * _currentZoom, MinZoom, MaxZoom);
            
            if (Math.Abs(newZoom - _currentZoom) < 0.001) return;
            
            // 计算缩放中心点调整（使缩放以鼠标位置为中心）
            var zoomRatio = newZoom / _currentZoom;
            var containerCenter = new Point(_previewContainer.Bounds.Width / 2, _previewContainer.Bounds.Height / 2);
            var mouseOffset = mousePos - containerCenter;
            
            // 调整平移以保持鼠标位置不变
            _currentTranslate = new Point(
                _currentTranslate.X - mouseOffset.X * (zoomRatio - 1),
                _currentTranslate.Y - mouseOffset.Y * (zoomRatio - 1)
            );
            
            _currentZoom = newZoom;
            ApplyTransform();
            
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标按下开始拖动
        /// </summary>
        private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_previewContainer == null) return;
            
            var point = e.GetCurrentPoint(_previewContainer);
            if (point.Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _lastDragPoint = point.Position;
                e.Pointer.Capture(_previewContainer);
                
                // 更改光标为抓取状态
                if (_previewContainer != null)
                {
                    _previewContainer.Cursor = new Cursor(StandardCursorType.Hand);
                }
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// 处理鼠标移动拖动
        /// </summary>
        private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging || _previewContainer == null) return;
            
            var currentPoint = e.GetPosition(_previewContainer);
            var delta = currentPoint - _lastDragPoint;
            
            _currentTranslate = new Point(
                _currentTranslate.X + delta.X,
                _currentTranslate.Y + delta.Y
            );
            
            _lastDragPoint = currentPoint;
            ApplyTransform();
            
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标释放结束拖动
        /// </summary>
        private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                e.Pointer.Capture(null);
                
                // 恢复光标
                if (_previewContainer != null)
                {
                    _previewContainer.Cursor = new Cursor(StandardCursorType.Arrow);
                }
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// 双击重置缩放
        /// </summary>
        private void OnPreviewDoubleTapped(object? sender, TappedEventArgs e)
        {
            ResetZoom();
            e.Handled = true;
        }

        /// <summary>
        /// 适应窗口大小按钮点击
        /// </summary>
        private void OnFitToWindowClick(object? sender, RoutedEventArgs e)
        {
            ResetZoom();
        }

        /// <summary>
        /// 放大按钮点击
        /// </summary>
        private void OnZoomInClick(object? sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Clamp(_currentZoom + ZoomStep * 2, MinZoom, MaxZoom);
            ApplyTransform();
        }

        /// <summary>
        /// 缩小按钮点击
        /// </summary>
        private void OnZoomOutClick(object? sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Clamp(_currentZoom - ZoomStep * 2, MinZoom, MaxZoom);
            ApplyTransform();
        }

        private async void OnSelectFilesClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择发票文件",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("发票文件")
                    {
                        Patterns = new[] { "*.pdf", "*.jpg", "*.jpeg", "*.png" }
                    }
                }
            });

            if (files != null && files.Count > 0 && DataContext is InvoiceImportViewModel viewModel)
            {
                await viewModel.UploadFilesCommand.ExecuteAsync(files);
            }
        }

        private async void OnDrop(object? sender, DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files == null) return;

            var validFiles = files
                .OfType<IStorageFile>()
                .Where(f =>
                {
                    var ext = System.IO.Path.GetExtension(f.Name).ToLower();
                    return ext == ".pdf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                })
                .ToList();

            if (validFiles.Count > 0 && DataContext is InvoiceImportViewModel viewModel)
            {
                await viewModel.UploadFilesCommand.ExecuteAsync(validFiles);
            }
        }
    }
}
