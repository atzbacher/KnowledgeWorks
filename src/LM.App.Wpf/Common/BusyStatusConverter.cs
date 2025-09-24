// src/LM.App.Wpf/Common/BusyStatusConverter.cs
using System;
using System.Globalization;

namespace LM.App.Wpf.Common
{
    [System.Windows.Data.ValueConversion(typeof(bool), typeof(string))]
    internal sealed class BusyStatusConverter : System.Windows.Data.IValueConverter
    {
        public string BusyText { get; init; } = "Working…";
        public string IdleText { get; init; } = "Ready";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? BusyText : IdleText;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}