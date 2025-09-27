#nullable enable

using System;
using System.Globalization;

namespace LM.App.Wpf.Common
{
    public sealed class NullableIntConverter : System.Windows.Data.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int number)
            {
                return number.ToString(culture);
            }

            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (int.TryParse(text, NumberStyles.Integer, culture, out var number))
                {
                    return Math.Max(0, number);
                }
            }

            return null;
        }
    }
}
