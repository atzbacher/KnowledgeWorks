#nullable enable
using System.Globalization;
using System.Windows.Data;

namespace LM.App.Wpf.Common
{
    [ValueConversion(typeof(bool), typeof(double))]
    public sealed class BooleanToOpacityConverter : IValueConverter
    {
        public double TrueOpacity { get; set; } = 1d;
        public double FalseOpacity { get; set; } = 0d;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                bool b => b ? TrueOpacity : FalseOpacity,
                bool? nb => nb.GetValueOrDefault() ? TrueOpacity : FalseOpacity,
                _ => FalseOpacity,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
