using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Converters
{
    public class InvoiceStatusToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is InvoiceStatus status)
            {
                return status switch
                {
                    InvoiceStatus.Pending => "待识别",
                    InvoiceStatus.Processing => "识别中...",
                    InvoiceStatus.Review => "待确认",
                    InvoiceStatus.Archived => "已归档",
                    _ => "未知状态"
                };
            }
            return "未知";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvoiceStatusToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is InvoiceStatus status && Application.Current != null)
            {
                string iconKey = status switch
                {
                    InvoiceStatus.Pending => "IconFile",
                    InvoiceStatus.Processing => "IconImport",
                    InvoiceStatus.Review => "IconFile", // Or maybe add an alert icon later
                    InvoiceStatus.Archived => "IconArchive",
                    _ => "IconFile"
                };

                if (Application.Current.TryGetResource(iconKey, null, out var resource) && resource is Geometry geometry)
                {
                    return geometry;
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvoiceStatusToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is InvoiceStatus status && Application.Current != null)
            {
                string colorKey = status switch
                {
                    InvoiceStatus.Pending => "TextSecondaryBrush",
                    InvoiceStatus.Processing => "PrimaryBrush",
                    InvoiceStatus.Review => "DangerBrush", // Using Danger for attention
                    InvoiceStatus.Archived => "SuccessBrush",
                    _ => "TextSecondaryBrush"
                };

                // Check if parameter is "Background" to return a lower opacity version or different brush
                if (parameter is string param && param == "Background")
                {
                    if (Application.Current.TryGetResource(colorKey, null, out var resource))
                    {
                        if (resource is ISolidColorBrush solidBrush)
                        {
                            // Return a new brush with 15% opacity
                            return new SolidColorBrush(solidBrush.Color, 0.15);
                        }
                    }
                }

                if (Application.Current.TryGetResource(colorKey, null, out var res))
                {
                    if (res is IBrush brush)
                    {
                        return brush;
                    }
                }
            }
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
