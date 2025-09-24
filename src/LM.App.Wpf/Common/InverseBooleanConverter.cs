using System;
using System.Globalization;

namespace LM.App.Wpf.Common
{
    [System.Windows.Data.ValueConversion(typeof(bool), typeof(bool))]
    internal sealed class InverseBooleanConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : System.Windows.Data.Binding.DoNothing;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : System.Windows.Data.Binding.DoNothing;
    }
}