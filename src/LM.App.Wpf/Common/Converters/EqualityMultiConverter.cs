#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LM.App.Wpf.Common.Converters;

internal sealed class EqualityMultiConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2)
        {
            return DependencyProperty.UnsetValue;
        }

        return Equals(values[0], values[1]);
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
