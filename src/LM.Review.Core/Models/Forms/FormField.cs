using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models.Forms;

public sealed class FormField
{
    private FormField(
        string id,
        string title,
        string? description,
        FormFieldType fieldType,
        IReadOnlyList<FormFieldOption> options,
        FormFieldValidation? validation,
        FormVisibilityRule? visibility,
        bool allowFreeTextFallback)
    {
        Id = id;
        Title = title;
        Description = description;
        FieldType = fieldType;
        Options = options;
        Validation = validation;
        Visibility = visibility;
        AllowFreeTextFallback = allowFreeTextFallback;
    }

    public string Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public FormFieldType FieldType { get; }

    public bool AllowFreeTextFallback { get; }

    public FormFieldValidation? Validation { get; }

    public FormVisibilityRule? Visibility { get; }

    public IReadOnlyList<FormFieldOption> Options { get; }

    public static FormField Create(
        string id,
        string title,
        FormFieldType fieldType,
        IEnumerable<FormFieldOption>? options = null,
        string? description = null,
        FormFieldValidation? validation = null,
        FormVisibilityRule? visibility = null,
        bool allowFreeTextFallback = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var normalizedId = FormIdentifier.Normalize(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            throw new InvalidOperationException("Field identifiers cannot be empty after normalization.");
        }

        var resolvedTitle = title.Trim();
        if (resolvedTitle.Length == 0)
        {
            throw new InvalidOperationException("Field titles cannot be empty.");
        }

        var resolvedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        var materializedOptions = new List<FormFieldOption>();
        if (options is not null)
        {
            foreach (var option in options)
            {
                ArgumentNullException.ThrowIfNull(option);
                materializedOptions.Add(option);
            }

            var duplicateOptionIds = materializedOptions
                .GroupBy(option => option.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateOptionIds.Count > 0)
            {
                throw new InvalidOperationException($"Field option identifiers must be unique. Duplicates: {string.Join(", ", duplicateOptionIds)}");
            }
        }

        if (fieldType is FormFieldType.SingleSelect or FormFieldType.MultiSelect)
        {
            if (materializedOptions.Count == 0)
            {
                throw new InvalidOperationException($"Field '{resolvedTitle}' must declare at least one option.");
            }
        }
        else if (materializedOptions.Count > 0)
        {
            throw new InvalidOperationException($"Field '{resolvedTitle}' does not support options for field type '{fieldType}'.");
        }

        if (allowFreeTextFallback && fieldType is not (FormFieldType.SingleSelect or FormFieldType.MultiSelect))
        {
            throw new InvalidOperationException("Free-text fallbacks are only supported for selection fields.");
        }

        return new FormField(
            normalizedId,
            resolvedTitle,
            resolvedDescription,
            fieldType,
            new ReadOnlyCollection<FormFieldOption>(materializedOptions),
            validation,
            visibility,
            allowFreeTextFallback);
    }
}

public sealed class FormFieldValidation
{
    private FormFieldValidation(
        FormValidationMode mode,
        decimal? minimum,
        decimal? maximum,
        DateTime? minimumDateUtc,
        DateTime? maximumDateUtc,
        string? expression)
    {
        Mode = mode;
        Minimum = minimum;
        Maximum = maximum;
        MinimumDateUtc = minimumDateUtc;
        MaximumDateUtc = maximumDateUtc;
        Expression = expression;
    }

    public FormValidationMode Mode { get; }

    public decimal? Minimum { get; }

    public decimal? Maximum { get; }

    public DateTime? MinimumDateUtc { get; }

    public DateTime? MaximumDateUtc { get; }

    public string? Expression { get; }

    public static FormFieldValidation CreateRequired() => new(FormValidationMode.Required, null, null, null, null, null);

    public static FormFieldValidation CreateNumericRange(decimal? minimum, decimal? maximum)
    {
        if (minimum is null && maximum is null)
        {
            throw new InvalidOperationException("Numeric range validation requires at least one bound.");
        }

        if (minimum is not null && maximum is not null && minimum > maximum)
        {
            throw new InvalidOperationException("Numeric range validation requires minimum to be less than or equal to maximum.");
        }

        return new FormFieldValidation(FormValidationMode.Range, minimum, maximum, null, null, null);
    }

    public static FormFieldValidation CreateDateRange(DateTime? minimumUtc, DateTime? maximumUtc)
    {
        if (minimumUtc is null && maximumUtc is null)
        {
            throw new InvalidOperationException("Date range validation requires at least one bound.");
        }

        DateTime? normalizedMin = null;
        DateTime? normalizedMax = null;

        if (minimumUtc is not null)
        {
            normalizedMin = EnsureUtc(minimumUtc.Value);
        }

        if (maximumUtc is not null)
        {
            normalizedMax = EnsureUtc(maximumUtc.Value);
        }

        if (normalizedMin is not null && normalizedMax is not null && normalizedMin > normalizedMax)
        {
            throw new InvalidOperationException("Date range validation requires minimum to be earlier than or equal to maximum.");
        }

        return new FormFieldValidation(FormValidationMode.Range, null, null, normalizedMin, normalizedMax, null);
    }

    public static FormFieldValidation CreateRegex(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        return new FormFieldValidation(FormValidationMode.Regex, null, null, null, null, pattern);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Utc => value,
            _ => value.ToUniversalTime()
        };
    }
}
