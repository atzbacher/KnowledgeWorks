using System;
using System.Globalization;
using System.Windows.Data;

namespace LM.App.Wpf.Common
{
    [ValueConversion(typeof(bool), typeof(bool))]
    internal sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : System.Windows.Data.Binding.DoNothing;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : System.Windows.Data.Binding.DoNothing;
    }
}