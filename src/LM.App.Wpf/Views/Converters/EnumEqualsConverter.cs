#nullable enable
using System;
using System.Globalization;

namespace LM.App.Wpf.Views.Converters
{
    [System.Windows.Data.ValueConversion(typeof(Enum), typeof(bool))]
    public sealed class EnumEqualsConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
            {
                return false;
            }

            if (value.Equals(parameter))
            {
                return true;
            }

            if (value is Enum enumValue)
            {
                if (parameter is Enum enumParameter && enumParameter.GetType() == enumValue.GetType())
                {
                    return enumValue.Equals(enumParameter);
                }

                if (parameter is string text)
                {
                    try
                    {
                        var parsed = (Enum)Enum.Parse(enumValue.GetType(), text, ignoreCase: true);
                        return enumValue.Equals(parsed);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is not null)
            {
                return parameter;
            }

            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
