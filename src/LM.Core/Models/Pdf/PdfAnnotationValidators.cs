using System;
namespace LM.Core.Models.Pdf;

internal static class PdfAnnotationValidators
{
    public static Guid EnsureValidId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Annotation id cannot be empty.", nameof(id));
        }

        return id;
    }

    public static DateTimeOffset EnsureValidModification(DateTimeOffset createdAt, DateTimeOffset modifiedAt)
    {
        return modifiedAt < createdAt ? createdAt : modifiedAt;
    }

    public static int EnsureValidPageIndex(int pageIndex)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index cannot be negative.");
        }

        return pageIndex;
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
