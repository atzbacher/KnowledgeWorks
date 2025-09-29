using System;

namespace LM.Core.Models.Pdf;

internal sealed record PdfAnnotationPreviewImage
{
    public PdfAnnotationPreviewImage(string mimeType, int width, int height, long lengthBytes, string? hash = null)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException("Mime type cannot be null or whitespace.", nameof(mimeType));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (lengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthBytes), "Length must be greater than zero.");
        }

        MimeType = mimeType.Trim();
        Width = width;
        Height = height;
        LengthBytes = lengthBytes;
        Hash = string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();
    }

    public string MimeType { get; }

    public int Width { get; }

    public int Height { get; }

    public long LengthBytes { get; }

    public string? Hash { get; }
}
