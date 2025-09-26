using System.Collections.Generic;
using System.Linq;
using LM.Review.Core.Models.Forms;

namespace LM.Review.Core.Validation;

public sealed partial class FormSchemaValidator
{
    private static void CollectSection(
        FormSection section,
        IDictionary<string, FormField> fieldIndex,
        IList<(FormVisibilityRule Rule, string OwnerId, string? SectionId, bool AppliesToSection)> visibilityRules,
        ICollection<FormSchemaIssue> issues)
    {
        if (!FormIdentifier.IsNormalized(section.Id))
        {
            issues.Add(FormSchemaIssue.Warning("Section.Id.NotNormalized", $"Section id '{section.Id}' is not normalized.", section.Id));
        }

        if (section.Visibility is not null)
        {
            visibilityRules.Add((section.Visibility, section.Id, section.Id, true));
        }

        foreach (var field in section.Fields)
        {
            if (!FormIdentifier.IsNormalized(field.Id))
            {
                issues.Add(FormSchemaIssue.Warning(
                    "Field.Id.NotNormalized",
                    $"Field id '{field.Id}' is not normalized.",
                    section.Id,
                    field.Id));
            }

            if (!fieldIndex.TryAdd(field.Id, field))
            {
                issues.Add(FormSchemaIssue.Error(
                    "Field.Id.Duplicate",
                    $"Field identifier '{field.Id}' is reused across the form.",
                    section.Id,
                    field.Id));
            }

            ValidateFieldDefinition(field, section, issues);

            if (field.Visibility is not null)
            {
                visibilityRules.Add((field.Visibility, field.Id, section.Id, false));
            }
        }

        foreach (var child in section.Children)
        {
            CollectSection(child, fieldIndex, visibilityRules, issues);
        }
    }

    private static void ValidateFieldDefinition(FormField field, FormSection section, ICollection<FormSchemaIssue> issues)
    {
        if (field.Options.Count == 0 && field.FieldType is FormFieldType.SingleSelect or FormFieldType.MultiSelect)
        {
            issues.Add(FormSchemaIssue.Error(
                "Field.Options.Empty",
                $"Selection field '{field.Id}' must define at least one option.",
                section.Id,
                field.Id));
        }

        if (field.FieldType is not (FormFieldType.SingleSelect or FormFieldType.MultiSelect) && field.Options.Count > 0)
        {
            issues.Add(FormSchemaIssue.Error(
                "Field.Options.Unsupported",
                $"Field type '{field.FieldType}' does not support options.",
                section.Id,
                field.Id));
        }

        if (field.FieldType == FormFieldType.SingleSelect)
        {
            var defaults = field.Options.Count(option => option.IsDefault);
            if (defaults > 1)
            {
                issues.Add(FormSchemaIssue.Error(
                    "Field.Options.Default",
                    $"Single-select field '{field.Id}' declares multiple default options.",
                    section.Id,
                    field.Id));
            }
        }

        foreach (var option in field.Options)
        {
            if (!FormIdentifier.IsNormalized(option.Id))
            {
                issues.Add(FormSchemaIssue.Warning(
                    "Option.Id.NotNormalized",
                    $"Option id '{option.Id}' is not normalized.",
                    section.Id,
                    field.Id));
            }
        }

        if (field.Validation is null)
        {
            return;
        }

        switch (field.Validation.Mode)
        {
            case FormValidationMode.Range:
            {
                var hasNumericBounds = field.Validation.Minimum is not null || field.Validation.Maximum is not null;
                var hasDateBounds = field.Validation.MinimumDateUtc is not null || field.Validation.MaximumDateUtc is not null;

                if (field.FieldType == FormFieldType.Numeric)
                {
                    if (!hasNumericBounds)
                    {
                        issues.Add(FormSchemaIssue.Warning(
                            "Field.Validation.Range.Bounds",
                            $"Numeric field '{field.Id}' declares range validation without bounds.",
                            section.Id,
                            field.Id));
                    }

                    if (hasDateBounds)
                    {
                        issues.Add(FormSchemaIssue.Error(
                            "Field.Validation.Range.BoundsType",
                            "Numeric fields cannot use date bounds.",
                            section.Id,
                            field.Id));
                    }
                }
                else if (field.FieldType == FormFieldType.Date)
                {
                    if (!hasDateBounds)
                    {
                        issues.Add(FormSchemaIssue.Warning(
                            "Field.Validation.Range.Bounds",
                            $"Date field '{field.Id}' declares range validation without bounds.",
                            section.Id,
                            field.Id));
                    }

                    if (hasNumericBounds)
                    {
                        issues.Add(FormSchemaIssue.Error(
                            "Field.Validation.Range.BoundsType",
                            "Date fields cannot use numeric bounds.",
                            section.Id,
                            field.Id));
                    }
                }
                else
                {
                    issues.Add(FormSchemaIssue.Error(
                        "Field.Validation.Range",
                        "Range validation is only supported for numeric or date fields.",
                        section.Id,
                        field.Id));
                }

                break;
            }
            case FormValidationMode.Regex:
                if (field.FieldType is not FormFieldType.Text)
                {
                    issues.Add(FormSchemaIssue.Error(
                        "Field.Validation.Regex",
                        "Regex validation is only supported for text fields.",
                        section.Id,
                        field.Id));
                }

                break;
        }
    }
}
