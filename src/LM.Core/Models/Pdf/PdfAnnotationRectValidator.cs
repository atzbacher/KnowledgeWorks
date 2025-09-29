using System;

namespace LM.Core.Models.Pdf;

internal static class PdfAnnotationRectValidator
{
    private const double MinNormalizedCoordinate = 0d;
    private const double MaxNormalizedCoordinate = 1d;

    public static void ThrowIfInvalidNormalized(double x, double y, double width, double height)
    {
        if (double.IsNaN(x) || double.IsInfinity(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be a finite value.");
        }

        if (double.IsNaN(y) || double.IsInfinity(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be a finite value.");
        }

        if (double.IsNaN(width) || double.IsInfinity(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be a finite value.");
        }

        if (double.IsNaN(height) || double.IsInfinity(height))
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be a finite value.");
        }

        if (width <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (x < MinNormalizedCoordinate || x > MaxNormalizedCoordinate)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be within the normalized range of 0 to 1.");
        }

        if (y < MinNormalizedCoordinate || y > MaxNormalizedCoordinate)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be within the normalized range of 0 to 1.");
        }

        if (x + width > MaxNormalizedCoordinate + double.Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "X plus width must not exceed 1.");
        }

        if (y + height > MaxNormalizedCoordinate + double.Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Y plus height must not exceed 1.");
        }
    }

    public static void ThrowIfInvalidPageSize(double pageWidth, double pageHeight)
    {
        if (double.IsNaN(pageWidth) || double.IsInfinity(pageWidth) || pageWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(pageWidth), "Page width must be a positive finite value.");
        }

        if (double.IsNaN(pageHeight) || double.IsInfinity(pageHeight) || pageHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(pageHeight), "Page height must be a positive finite value.");
        }
    }
}
