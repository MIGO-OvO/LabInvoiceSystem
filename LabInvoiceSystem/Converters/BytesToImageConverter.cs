using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace LabInvoiceSystem.Converters
{
    public class BytesToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    return new Bitmap(stream);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
