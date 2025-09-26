using System;
using System.Collections.Generic;
using LM.Review.Core.Models.Forms;

namespace LM.Review.Core.Validation;

public sealed partial class FormSchemaValidator
{
    public IReadOnlyList<FormSchemaIssue> Validate(ExtractionForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var issues = new List<FormSchemaIssue>();
        if (!FormIdentifier.IsNormalized(form.Id))
        {
            issues.Add(FormSchemaIssue.Warning("Form.Id.NotNormalized", $"Form id '{form.Id}' is not normalized."));
        }

        var fieldIndex = new Dictionary<string, FormField>(StringComparer.Ordinal);
        var visibilityRules = new List<(FormVisibilityRule Rule, string OwnerId, string? SectionId, bool AppliesToSection)>();

        foreach (var rootSection in form.Sections)
        {
            CollectSection(rootSection, fieldIndex, visibilityRules, issues);
        }

        foreach (var pending in visibilityRules)
        {
            if (!fieldIndex.ContainsKey(pending.Rule.SourceFieldId))
            {
                issues.Add(FormSchemaIssue.Error(
                    "Visibility.Source.Missing",
                    $"Visibility rule references unknown field '{pending.Rule.SourceFieldId}'.",
                    pending.SectionId,
                    pending.AppliesToSection ? null : pending.OwnerId));
                continue;
            }

            if (string.Equals(pending.Rule.SourceFieldId, pending.OwnerId, StringComparison.Ordinal))
            {
                issues.Add(FormSchemaIssue.Error(
                    "Visibility.SelfReference",
                    "Visibility rules cannot depend on the field they control.",
                    pending.SectionId,
                    pending.AppliesToSection ? null : pending.OwnerId));
            }
        }

        return issues.AsReadOnly();
    }

    public IReadOnlyList<FormSchemaIssue> Validate(ExtractionFormVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        var issues = new List<FormSchemaIssue>();
        if (!FormIdentifier.IsNormalized(version.VersionId))
        {
            issues.Add(FormSchemaIssue.Warning("Version.Id.NotNormalized", $"Version id '{version.VersionId}' is not normalized."));
        }

        if (string.IsNullOrWhiteSpace(version.CreatedBy))
        {
            issues.Add(FormSchemaIssue.Error("Version.CreatedBy.Missing", "Version metadata must include a creator."));
        }

        if (version.CreatedUtc.Kind != DateTimeKind.Utc)
        {
            issues.Add(FormSchemaIssue.Warning("Version.CreatedUtc.Kind", "CreatedUtc timestamps should be stored as UTC."));
        }

        issues.AddRange(Validate(version.Form));
        return issues.AsReadOnly();
    }
}
