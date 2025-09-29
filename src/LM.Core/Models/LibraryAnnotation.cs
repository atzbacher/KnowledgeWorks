#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LM.Core.Models;

public enum LibraryAnnotationType
{
    Highlight,
    Note,
    Rectangle,
    Underline
}

public readonly record struct LibraryAnnotationGeometry(float X, float Y, float Width, float Height);

public sealed record class LibraryAnnotation
{
    public LibraryAnnotation(Guid annotationId,
                             string entryId,
                             string attachmentId,
                             int pageNumber,
                             LibraryAnnotationGeometry geometry,
                             LibraryAnnotationType annotationType,
                             string? colorKey,
                             IEnumerable<string>? tags,
                             string? title,
                             string? note,
                             string? meaning,
                             string createdBy,
                             DateTime createdAtUtc,
                             string? lastModifiedBy,
                             DateTime? lastModifiedUtc)
    {
        if (annotationId == Guid.Empty)
        {
            throw new ArgumentException("Annotation identifier cannot be empty.", nameof(annotationId));
        }

        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("Entry identifier is required.", nameof(entryId));
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number must be positive.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("Created by value is required.", nameof(createdBy));
        }

        AnnotationId = annotationId;
        EntryId = entryId.Trim();
        AttachmentId = (attachmentId ?? string.Empty).Trim();
        PageNumber = pageNumber;
        Geometry = geometry;
        AnnotationType = annotationType;
        ColorKey = string.IsNullOrWhiteSpace(colorKey) ? null : colorKey.Trim();
        Tags = tags?.Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(static tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Meaning = string.IsNullOrWhiteSpace(meaning) ? null : meaning.Trim();
        CreatedBy = createdBy.Trim();
        CreatedAtUtc = createdAtUtc;
        LastModifiedBy = string.IsNullOrWhiteSpace(lastModifiedBy) ? null : lastModifiedBy.Trim();
        LastModifiedUtc = lastModifiedUtc;
    }

    public Guid AnnotationId { get; init; }

    public string EntryId { get; init; }

    public string AttachmentId { get; init; }

    public int PageNumber { get; init; }

    public LibraryAnnotationGeometry Geometry { get; init; }

    public LibraryAnnotationType AnnotationType { get; init; }

    public string? ColorKey { get; init; }

    public IReadOnlyList<string> Tags { get; init; }

    public string? Title { get; init; }

    public string? Note { get; init; }

    public string? Meaning { get; init; }

    public string CreatedBy { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string? LastModifiedBy { get; init; }

    public DateTime? LastModifiedUtc { get; init; }
}
