
using System;
using System.Globalization;
using System.Windows.Data;
using LM.App.Wpf.ViewModels.Pdf;

namespace LM.App.Wpf.Views.Pdf
{
    /// <summary>
    /// Converts an annotation and color name into a PdfAnnotationColorCommandParameter.
    /// </summary>
    public sealed class AnnotationColorParameterConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return null;
            }

            // values[0] is the PdfAnnotation (from DataContext binding)
            // values[1] is the color name (from MenuItem.Tag)

            if (values[0] is not PdfAnnotation annotation)
            {
                return null;
            }

            var colorName = values[1] as string;

            return new PdfAnnotationColorCommandParameter
            {
                Annotation = annotation,
                ColorName = colorName
            };
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}