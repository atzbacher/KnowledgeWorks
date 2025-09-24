#nullable enable
using System.Globalization;

namespace LM.App.Wpf.Common
{
    [System.Windows.Data.ValueConversion(typeof(bool), typeof(double))]
    public sealed class BooleanToOpacityConverter : System.Windows.Data.IValueConverter
    {
        public double TrueOpacity { get; set; } = 1d;
        public double FalseOpacity { get; set; } = 0d;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? TrueOpacity : FalseOpacity;
            }

            return FalseOpacity;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
