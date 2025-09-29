using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class MuPdfAnnotationViewModel : ObservableObject
{
    public MuPdfAnnotationViewModel(int pageNumber, NormalizedRectangle region)
    {
        if (pageNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        PageNumber = pageNumber;
        Region = region;
        CreatedAtUtc = DateTime.UtcNow;
        Note = string.Empty;
        PixelLeft = 0d;
        PixelTop = 0d;
        PixelWidth = 0d;
        PixelHeight = 0d;
    }

    public int PageNumber { get; }

    public NormalizedRectangle Region { get; }

    public DateTime CreatedAtUtc { get; }

    [ObservableProperty]
    private string note;

    [ObservableProperty]
    private double pixelLeft;

    [ObservableProperty]
    private double pixelTop;

    [ObservableProperty]
    private double pixelWidth;

    [ObservableProperty]
    private double pixelHeight;

    public string DisplayTitle => $"Page {PageNumber + 1}: {Region.Width:P0} × {Region.Height:P0}";

    public string TimestampDisplay => CreatedAtUtc.ToLocalTime().ToString("g");

    public void UpdatePixelMetrics(double pageWidth, double pageHeight)
    {
        PixelLeft = Math.Round(Region.X * pageWidth, 2);
        PixelTop = Math.Round(Region.Y * pageHeight, 2);
        PixelWidth = Math.Round(Region.Width * pageWidth, 2);
        PixelHeight = Math.Round(Region.Height * pageHeight, 2);
    }
}

internal readonly struct NormalizedRectangle
{
    public NormalizedRectangle(double x, double y, double width, double height)
    {
        X = Clamp(x);
        Y = Clamp(y);
        Width = Clamp(width);
        Height = Clamp(height);
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public static NormalizedRectangle FromPixels(System.Windows.Rect pixels, System.Windows.Size canvasSize)
    {
        if (canvasSize.Width <= 0d || canvasSize.Height <= 0d)
        {
            return new NormalizedRectangle(0d, 0d, 1d, 1d);
        }

        var left = Clamp(pixels.X / canvasSize.Width);
        var top = Clamp(pixels.Y / canvasSize.Height);
        var width = Clamp(pixels.Width / canvasSize.Width);
        var height = Clamp(pixels.Height / canvasSize.Height);

        return new NormalizedRectangle(left, top, width, height);
    }

    public System.Windows.Rect ToPixels(System.Windows.Size canvasSize)
    {
        var x = Clamp(X) * canvasSize.Width;
        var y = Clamp(Y) * canvasSize.Height;
        var width = Clamp(Width) * canvasSize.Width;
        var height = Clamp(Height) * canvasSize.Height;
        return new System.Windows.Rect(x, y, width, height);
    }

    private static double Clamp(double value)
    {
        if (double.IsNaN(value))
        {
            return 0d;
        }

        return Math.Max(0d, Math.Min(1d, value));
    }
}
