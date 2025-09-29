namespace LM.Core.Models.Pdf;

internal sealed record PdfAnnotationRect : IPdfAnnotationRect
{
    public PdfAnnotationRect(double x, double y, double width, double height)
    {
        PdfAnnotationRectValidator.ThrowIfInvalidNormalized(x, y, width, height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public PdfAnnotationRect WithNormalizedCoordinates(double x, double y, double width, double height)
    {
        return new PdfAnnotationRect(x, y, width, height);
    }

    public PdfAnnotationRect Translate(double deltaX, double deltaY)
    {
        var translatedX = X + deltaX;
        var translatedY = Y + deltaY;

        PdfAnnotationRectValidator.ThrowIfInvalidNormalized(translatedX, translatedY, Width, Height);

        return new PdfAnnotationRect(translatedX, translatedY, Width, Height);
    }

    public static PdfAnnotationRect FromAbsolute(double x, double y, double width, double height, double pageWidth, double pageHeight)
    {
        return PdfAnnotationRectNormalizer.Normalize(x, y, width, height, pageWidth, pageHeight);
    }
}
