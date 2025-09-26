using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models.Forms;

public sealed class FormSection
{
    private FormSection(
        string id,
        string title,
        IReadOnlyList<FormField> fields,
        IReadOnlyList<FormSection> children,
        bool isRepeatable,
        string? description,
        FormVisibilityRule? visibility)
    {
        Id = id;
        Title = title;
        Fields = fields;
        Children = children;
        IsRepeatable = isRepeatable;
        Description = description;
        Visibility = visibility;
    }

    public string Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public bool IsRepeatable { get; }

    public FormVisibilityRule? Visibility { get; }

    public IReadOnlyList<FormField> Fields { get; }

    public IReadOnlyList<FormSection> Children { get; }

    public static FormSection Create(
        string id,
        string title,
        IEnumerable<FormField> fields,
        IEnumerable<FormSection>? children = null,
        bool isRepeatable = false,
        string? description = null,
        FormVisibilityRule? visibility = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(fields);

        var normalizedId = FormIdentifier.Normalize(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            throw new InvalidOperationException("Section identifiers cannot be empty after normalization.");
        }

        var resolvedTitle = title.Trim();
        if (resolvedTitle.Length == 0)
        {
            throw new InvalidOperationException("Section titles cannot be empty.");
        }

        var resolvedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        var materializedFields = new List<FormField>();
        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field);
            materializedFields.Add(field);
        }

        if (materializedFields.Count == 0)
        {
            throw new InvalidOperationException($"Section '{resolvedTitle}' must contain at least one field.");
        }

        var duplicateFieldIds = materializedFields
            .GroupBy(field => field.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateFieldIds.Count > 0)
        {
            throw new InvalidOperationException($"Field identifiers must be unique. Duplicates: {string.Join(", ", duplicateFieldIds)}");
        }

        var materializedChildren = new List<FormSection>();
        if (children is not null)
        {
            foreach (var child in children)
            {
                ArgumentNullException.ThrowIfNull(child);
                materializedChildren.Add(child);
            }

            var duplicateChildIds = materializedChildren
                .GroupBy(section => section.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateChildIds.Count > 0)
            {
                throw new InvalidOperationException($"Child section identifiers must be unique. Duplicates: {string.Join(", ", duplicateChildIds)}");
            }
        }

        return new FormSection(
            normalizedId,
            resolvedTitle,
            new ReadOnlyCollection<FormField>(materializedFields),
            new ReadOnlyCollection<FormSection>(materializedChildren),
            isRepeatable,
            resolvedDescription,
            visibility);
    }
}
