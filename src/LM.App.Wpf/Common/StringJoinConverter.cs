#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LM.App.Wpf.Common
{
    public sealed class StringJoinConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
                return string.Empty;

            var separator = parameter as string ?? "; ";

            if (value is string str)
                return str;

            if (value is not IEnumerable enumerable)
                return value.ToString() ?? string.Empty;

            var items = enumerable
                .Cast<object?>()
                .Select(item => item switch
                {
                    null => null,
                    string s => s,
                    _ => item.ToString()
                })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToArray();

            return items.Length == 0 ? string.Empty : string.Join(separator, items);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
