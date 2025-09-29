#nullable enable
using System;
using System.Globalization;

namespace LM.App.Wpf.Views.Library.Converters;

internal sealed class EnumEqualsConverter : System.Windows.Data.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        var valueType = value.GetType();
        if (!valueType.IsEnum)
        {
            return false;
        }

        if (parameter is string parameterString)
        {
            try
            {
                var parsed = Enum.Parse(valueType, parameterString, ignoreCase: true);
                return value.Equals(parsed);
            }
            catch
            {
                return false;
            }
        }

        if (parameter.GetType() == valueType)
        {
            return value.Equals(parameter);
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsEnum || value is not bool isChecked || !isChecked || parameter is null)
        {
            return System.Windows.Data.Binding.DoNothing;
        }

        if (parameter is string parameterString)
        {
            return Enum.Parse(targetType, parameterString, ignoreCase: true);
        }

        return parameter;
    }
}
