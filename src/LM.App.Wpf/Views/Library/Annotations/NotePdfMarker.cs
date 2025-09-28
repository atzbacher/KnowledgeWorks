#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using PdfiumViewer;
using PdfiumViewer.Core;

namespace LM.App.Wpf.Views.Library.Annotations;

internal sealed class NotePdfMarker : IPdfMarker
{
    private readonly RectangleF _pdfBounds;
    private readonly string _note;
    private readonly System.Windows.Media.Color _fillColor;
    private readonly System.Windows.Media.Color _borderColor;

    public NotePdfMarker(int page,
                         RectangleF pdfBounds,
                         string note,
                         System.Windows.Media.Color fillColor,
                         System.Windows.Media.Color borderColor)
    {
        Page = page;
        _pdfBounds = pdfBounds;
        _note = note;
        _fillColor = fillColor;
        _borderColor = borderColor;
    }

    public int Page { get; }

    public void Draw(PdfRenderer renderer, System.Windows.Media.DrawingContext graphics)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(graphics);

        var bounds = ConvertToRect(renderer.RectangleFromPdf(Page, _pdfBounds), renderer);
        var background = new System.Windows.Media.SolidColorBrush(_fillColor)
        {
            Opacity = 0.65,
        };
        background.Freeze();

        var borderBrush = new System.Windows.Media.SolidColorBrush(_borderColor);
        borderBrush.Freeze();

        var borderPen = new System.Windows.Media.Pen(borderBrush, 1.2);
        borderPen.Freeze();

        graphics.DrawRectangle(background, borderPen, bounds);

        if (string.IsNullOrWhiteSpace(_note))
        {
            return;
        }

        var typeface = new System.Windows.Media.Typeface("Segoe UI");
        var formatted = new System.Windows.Media.FormattedText(
            _note,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            12,
            System.Windows.Media.Brushes.Black,
            System.Windows.Media.VisualTreeHelper.GetDpi(renderer).PixelsPerDip);

        var padding = new System.Windows.Size(8, 4);
        var textRect = new System.Windows.Rect(
            bounds.X + padding.Width,
            bounds.Y + padding.Height,
            Math.Min(bounds.Width - padding.Width * 2, formatted.Width),
            Math.Min(bounds.Height - padding.Height * 2, formatted.Height));

        graphics.DrawText(formatted, new System.Windows.Point(textRect.X, textRect.Y));
    }

    private static System.Windows.Rect ConvertToRect(System.Drawing.Rectangle rectangle, System.Windows.Media.Visual visual)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(visual);
        var x = rectangle.X / dpi.DpiScaleX;
        var y = rectangle.Y / dpi.DpiScaleY;
        var width = rectangle.Width / dpi.DpiScaleX;
        var height = rectangle.Height / dpi.DpiScaleY;
        return new System.Windows.Rect(x, y, width, height);
    }
}
