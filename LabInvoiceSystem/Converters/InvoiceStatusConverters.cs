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
        private const string DefaultGeometryData = "M6 2C4.89543 2 4 2.89543 4 4V20C4 21.1046 4.89543 22 6 22H18C19.1046 22 20 21.1046 20 20V8L14 2H6ZM13 3.5L18.5 9H13V3.5Z";

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not InvoiceStatus status)
            {
                return GetFallbackGeometry();
            }

            var iconKey = status switch
            {
                InvoiceStatus.Pending => "IconFile",
                InvoiceStatus.Processing => "IconImport",
                InvoiceStatus.Review => "IconFile",
                InvoiceStatus.Archived => "IconArchive",
                _ => "IconFile"
            };

            return GetGeometry(iconKey) ?? GetGeometry("IconFile") ?? GetFallbackGeometry();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Geometry? GetGeometry(string resourceKey)
        {
            if (Application.Current == null)
            {
                return null;
            }

            if (Application.Current.TryGetResource(resourceKey, null, out var resource) && resource is Geometry geometry)
            {
                return geometry;
            }

            return null;
        }

        private static Geometry GetFallbackGeometry()
        {
            return Geometry.Parse(DefaultGeometryData);
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
