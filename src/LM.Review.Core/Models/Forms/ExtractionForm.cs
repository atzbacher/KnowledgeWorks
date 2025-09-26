using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace LM.Review.Core.Models.Forms;

public sealed class ExtractionForm
{
    private ExtractionForm(
        string id,
        string name,
        IReadOnlyList<FormSection> sections,
        string? description,
        string? category)
    {
        Id = id;
        Name = name;
        Sections = sections;
        Description = description;
        Category = category;
    }

    public string Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public string? Category { get; }

    public IReadOnlyList<FormSection> Sections { get; }

    public static ExtractionForm Create(
        string id,
        string name,
        IEnumerable<FormSection> sections,
        string? description = null,
        string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(sections);

        var normalizedId = FormIdentifier.Normalize(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            throw new InvalidOperationException("Extraction form identifiers cannot be empty after normalization.");
        }

        var resolvedName = name.Trim();
        if (resolvedName.Length == 0)
        {
            throw new InvalidOperationException("Extraction form names cannot be empty.");
        }

        var resolvedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var resolvedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

        var materializedSections = new List<FormSection>();
        foreach (var section in sections)
        {
            ArgumentNullException.ThrowIfNull(section);
            materializedSections.Add(section);
        }

        if (materializedSections.Count == 0)
        {
            throw new InvalidOperationException("Extraction forms must declare at least one section.");
        }

        var duplicateSectionIds = materializedSections
            .GroupBy(section => section.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateSectionIds.Count > 0)
        {
            throw new InvalidOperationException($"Section identifiers must be unique. Duplicates: {string.Join(", ", duplicateSectionIds)}");
        }

        return new ExtractionForm(
            normalizedId,
            resolvedName,
            new ReadOnlyCollection<FormSection>(materializedSections),
            resolvedDescription,
            resolvedCategory);
    }
}

public static class FormIdentifier
{
    private static readonly Regex s_invalidCharacters = new("[^A-Za-z0-9_-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex s_repeatedSeparators = new("[-_]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var trimmed = identifier.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var collapsed = s_invalidCharacters.Replace(trimmed, "-");
        collapsed = s_repeatedSeparators.Replace(collapsed, "-");
        collapsed = collapsed.Trim('-');

        return collapsed.ToLowerInvariant();
    }

    public static bool IsNormalized(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        return string.Equals(identifier, Normalize(identifier), StringComparison.Ordinal);
    }
}
