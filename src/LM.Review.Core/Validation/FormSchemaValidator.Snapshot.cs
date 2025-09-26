using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LM.Review.Core.Models.Forms;

namespace LM.Review.Core.Validation;

public sealed partial class FormSchemaValidator
{
    public IReadOnlyList<FormSchemaIssue> Validate(ExtractionFormSnapshot snapshot, ExtractionFormVersion version)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(version);

        var issues = new List<FormSchemaIssue>();
        if (!string.Equals(snapshot.FormId, version.Form.Id, StringComparison.Ordinal))
        {
            issues.Add(FormSchemaIssue.Error(
                "Snapshot.Form.Mismatch",
                $"Snapshot references form '{snapshot.FormId}' but version is '{version.Form.Id}'."));
        }

        if (!string.Equals(snapshot.VersionId, version.VersionId, StringComparison.Ordinal))
        {
            issues.Add(FormSchemaIssue.Error(
                "Snapshot.Version.Mismatch",
                $"Snapshot references version '{snapshot.VersionId}' but definition is '{version.VersionId}'."));
        }

        if (snapshot.CapturedUtc.Kind != DateTimeKind.Utc)
        {
            issues.Add(FormSchemaIssue.Warning("Snapshot.CapturedUtc.Kind", "CapturedUtc timestamps should be stored as UTC."));
        }

        var fieldIndex = BuildFieldIndex(version.Form);

        foreach (var kvp in snapshot.Values)
        {
            if (!fieldIndex.TryGetValue(kvp.Key, out var field))
            {
                issues.Add(FormSchemaIssue.Error(
                    "Snapshot.Field.Unknown",
                    $"Snapshot includes value for unknown field '{kvp.Key}'.",
                    fieldId: kvp.Key));
                continue;
            }

            var optionIssue = ValidateFieldValue(field, kvp.Value);
            if (optionIssue is not null)
            {
                issues.Add(optionIssue);
            }
        }

        foreach (var pair in fieldIndex)
        {
            if (pair.Value.Validation?.Mode != FormValidationMode.Required)
            {
                continue;
            }

            if (!snapshot.Values.TryGetValue(pair.Key, out var value) || !HasValue(value))
            {
                issues.Add(FormSchemaIssue.Error(
                    "Snapshot.Field.Required",
                    $"Required field '{pair.Key}' is missing a value.",
                    fieldId: pair.Key));
            }
        }

        return issues.AsReadOnly();
    }

    private static Dictionary<string, FormField> BuildFieldIndex(ExtractionForm form)
    {
        var index = new Dictionary<string, FormField>(StringComparer.Ordinal);
        var sections = new Stack<FormSection>(form.Sections.Reverse());
        while (sections.Count > 0)
        {
            var section = sections.Pop();
            foreach (var field in section.Fields)
            {
                if (!index.ContainsKey(field.Id))
                {
                    index[field.Id] = field;
                }
            }

            foreach (var child in section.Children)
            {
                sections.Push(child);
            }
        }

        return index;
    }

    private static FormSchemaIssue? ValidateFieldValue(FormField field, object? value)
    {
        if (value is null)
        {
            return null;
        }

        return field.FieldType switch
        {
            FormFieldType.SingleSelect => ValidateSingleSelect(field, value),
            FormFieldType.MultiSelect => ValidateMultiSelect(field, value),
            FormFieldType.Numeric => ValidateNumeric(field, value),
            FormFieldType.Date => ValidateDate(field, value),
            _ => null
        };
    }

    private static FormSchemaIssue? ValidateSingleSelect(FormField field, object? value)
    {
        if (value is string singleValue)
        {
            if (IsOptionMatch(field, singleValue) || field.AllowFreeTextFallback)
            {
                return null;
            }

            return FormSchemaIssue.Error(
                "Snapshot.Field.OptionMismatch",
                $"Value '{singleValue}' is not a valid option for field '{field.Id}'.",
                fieldId: field.Id);
        }

        return FormSchemaIssue.Error(
            "Snapshot.Field.TypeMismatch",
            $"Field '{field.Id}' expects a single option identifier.",
            fieldId: field.Id);
    }

    private static FormSchemaIssue? ValidateMultiSelect(FormField field, object? value)
    {
        if (value is string singleValue)
        {
            return ValidateSingleSelect(field, singleValue);
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                if (item is not string stringItem)
                {
                    return FormSchemaIssue.Error(
                        "Snapshot.Field.TypeMismatch",
                        $"Field '{field.Id}' expects option identifiers.",
                        fieldId: field.Id);
                }

                if (!IsOptionMatch(field, stringItem) && !field.AllowFreeTextFallback)
                {
                    return FormSchemaIssue.Error(
                        "Snapshot.Field.OptionMismatch",
                        $"Value '{stringItem}' is not a valid option for field '{field.Id}'.",
                        fieldId: field.Id);
                }
            }

            return null;
        }

        return FormSchemaIssue.Error(
            "Snapshot.Field.TypeMismatch",
            $"Field '{field.Id}' expects a collection of option identifiers.",
            fieldId: field.Id);
    }

    private static FormSchemaIssue? ValidateNumeric(FormField field, object? value)
    {
        if (value is IConvertible convertible)
        {
            try
            {
                var numericValue = convertible.ToDecimal(CultureInfo.InvariantCulture);
                if (field.Validation?.Mode == FormValidationMode.Range)
                {
                    if (field.Validation.Minimum is not null && numericValue < field.Validation.Minimum)
                    {
                        return FormSchemaIssue.Error(
                            "Snapshot.Field.Range",
                            $"Value {numericValue} is less than the minimum for field '{field.Id}'.",
                            fieldId: field.Id);
                    }

                    if (field.Validation.Maximum is not null && numericValue > field.Validation.Maximum)
                    {
                        return FormSchemaIssue.Error(
                            "Snapshot.Field.Range",
                            $"Value {numericValue} exceeds the maximum for field '{field.Id}'.",
                            fieldId: field.Id);
                    }
                }

                return null;
            }
            catch
            {
                // Fall through and return mismatch issue below.
            }
        }

        return FormSchemaIssue.Error(
            "Snapshot.Field.TypeMismatch",
            $"Field '{field.Id}' expects a numeric value.",
            fieldId: field.Id);
    }

    private static FormSchemaIssue? ValidateDate(FormField field, object? value)
    {
        DateTime? normalizedDate = value switch
        {
            DateTime dateTime => dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                _ => dateTime.ToUniversalTime()
            },
            DateOnly dateOnly => DateTime.SpecifyKind(dateOnly.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            _ => null
        };

        if (normalizedDate is null)
        {
            return FormSchemaIssue.Error(
                "Snapshot.Field.TypeMismatch",
                $"Field '{field.Id}' expects a date value.",
                fieldId: field.Id);
        }

        if (field.Validation?.Mode == FormValidationMode.Range)
        {
            if (field.Validation.MinimumDateUtc is not null && normalizedDate < field.Validation.MinimumDateUtc)
            {
                return FormSchemaIssue.Error(
                    "Snapshot.Field.Range",
                    $"Value '{normalizedDate.Value:u}' is earlier than the minimum for field '{field.Id}'.",
                    fieldId: field.Id);
            }

            if (field.Validation.MaximumDateUtc is not null && normalizedDate > field.Validation.MaximumDateUtc)
            {
                return FormSchemaIssue.Error(
                    "Snapshot.Field.Range",
                    $"Value '{normalizedDate.Value:u}' is later than the maximum for field '{field.Id}'.",
                    fieldId: field.Id);
            }
        }

        return null;
    }

    private static bool HasValue(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
            {
                return true;
            }

            return false;
        }

        return true;
    }

    private static bool IsOptionMatch(FormField field, string candidate)
    {
        var normalized = candidate.Trim();
        return field.Options.Any(option => string.Equals(option.Id, normalized, StringComparison.Ordinal));
    }
}
