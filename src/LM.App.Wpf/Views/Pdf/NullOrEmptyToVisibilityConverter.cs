using System;
using System.Globalization;

namespace LM.App.Wpf.Views.Pdf
{
    public sealed class NullOrEmptyToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public System.Windows.Visibility EmptyState { get; set; } = System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility NonEmptyState { get; set; } = System.Windows.Visibility.Visible;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return NonEmptyState;
            }

            return EmptyState;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NullOrEmptyToVisibilityConverter does not support ConvertBack.");
        }
    }
}
