#nullable enable

namespace LM.Review.Core.Models;

public sealed class ReviewProjectMetadata
{
    private ReviewProjectMetadata(ReviewTemplateKind template, string notes)
    {
        Template = template;
        Notes = notes;
    }

    public ReviewTemplateKind Template { get; }

    public string Notes { get; }

    public static ReviewProjectMetadata Create(ReviewTemplateKind template, string? notes)
    {
        var normalizedNotes = string.IsNullOrWhiteSpace(notes)
            ? string.Empty
            : notes.Trim();

        return new ReviewProjectMetadata(template, normalizedNotes);
    }
}
