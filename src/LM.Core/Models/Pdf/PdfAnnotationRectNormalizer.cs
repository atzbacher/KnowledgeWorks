namespace LM.Core.Models.Pdf;

internal static class PdfAnnotationRectNormalizer
{
    public static PdfAnnotationRect Normalize(double x, double y, double width, double height, double pageWidth, double pageHeight)
    {
        PdfAnnotationRectValidator.ThrowIfInvalidPageSize(pageWidth, pageHeight);

        var normalizedX = x / pageWidth;
        var normalizedY = y / pageHeight;
        var normalizedWidth = width / pageWidth;
        var normalizedHeight = height / pageHeight;

        return new PdfAnnotationRect(normalizedX, normalizedY, normalizedWidth, normalizedHeight);
    }
}
