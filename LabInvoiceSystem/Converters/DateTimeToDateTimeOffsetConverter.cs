using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LabInvoiceSystem.Converters
{
    public class DateTimeToDateTimeOffsetConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                }

                return new DateTimeOffset(dt);
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTimeOffset dto)
            {
                return dto.LocalDateTime.Date;
            }

            return DateTime.Now.Date;
        }
    }
}
