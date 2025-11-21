using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LabInvoiceSystem.Converters
{
    public class PaymentMethodColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string paymentMethod)
            {
                return paymentMethod switch
                {
                    "公务卡" => new SolidColorBrush(Color.FromRgb(59, 130, 246)), // 蓝色
                    "现金" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),    // 绿色
                    _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))        // 灰色
                };
            }

            return new SolidColorBrush(Color.FromRgb(156, 163, 175));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
