using System;

namespace LM.Review.Core.Models.Forms;

public sealed class FormFieldOption
{
    private FormFieldOption(string id, string label, string? description, bool isDefault)
    {
        Id = id;
        Label = label;
        Description = description;
        IsDefault = isDefault;
    }

    public string Id { get; }

    public string Label { get; }

    public string? Description { get; }

    public bool IsDefault { get; }

    public static FormFieldOption Create(string id, string label, string? description = null, bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var normalizedId = FormIdentifier.Normalize(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            throw new InvalidOperationException("Field option identifiers cannot be empty after normalization.");
        }

        var resolvedLabel = label.Trim();
        if (resolvedLabel.Length == 0)
        {
            throw new InvalidOperationException("Field option labels cannot be empty.");
        }

        var resolvedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        return new FormFieldOption(normalizedId, resolvedLabel, resolvedDescription, isDefault);
    }
}
