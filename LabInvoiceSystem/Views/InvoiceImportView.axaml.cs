using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LabInvoiceSystem.ViewModels;

namespace LabInvoiceSystem.Views
{
    public partial class InvoiceImportView : UserControl
    {
        public InvoiceImportView()
        {
            InitializeComponent();
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
    }
}
