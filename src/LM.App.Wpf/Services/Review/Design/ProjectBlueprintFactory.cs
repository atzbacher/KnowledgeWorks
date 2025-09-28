#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LM.App.Wpf.Services.Review;
using LM.Core.Models;
using LM.Review.Core.Models;

namespace LM.App.Wpf.Services.Review.Design;

internal static class ProjectBlueprintFactory
{
    public static ProjectBlueprint Create(
        LitSearchRunSelection selection,
        Entry? entry,
        IReadOnlyList<string> checkedEntryIds,
        string userName)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(checkedEntryIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var now = DateTimeOffset.UtcNow;
        var projectId = $"review-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}";
        var projectName = ResolveProjectName(entry, selection.EntryId);

        var normalizedCheckedIds = checkedEntryIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .ToArray();

        var stages = new List<StageBlueprint>
        {
            new(
                $"stage-def-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}",
                "Title screening",
                ReviewStageType.TitleScreening,
                primaryReviewers: 2,
                secondaryReviewers: 0,
                requiresConsensus: true,
                minimumAgreements: 2,
                escalateOnDisagreement: true,
                StageDisplayProfileFactory.CreateDefault(ReviewStageType.TitleScreening)),
            new(
                $"stage-def-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}",
                "Quality assurance",
                ReviewStageType.QualityAssurance,
                primaryReviewers: 1,
                secondaryReviewers: 0,
                requiresConsensus: false,
                minimumAgreements: 0,
                escalateOnDisagreement: false,
                StageDisplayProfileFactory.CreateDefault(ReviewStageType.QualityAssurance))
        };

        return new ProjectBlueprint(
            projectId,
            projectName,
            now,
            userName,
            selection.EntryId,
            selection.RunId,
            normalizedCheckedIds,
            selection.HookRelativePath,
            ReviewTemplateKind.Picos,
            string.Empty,
            stages);
    }

    private static string ResolveProjectName(Entry? entry, string fallbackId)
    {
        if (entry is null)
        {
            return $"Review {fallbackId}";
        }

        var label = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Title : entry.DisplayName;
        if (string.IsNullOrWhiteSpace(label))
        {
            return $"Review {entry.Id}";
        }

        return $"Review – {label.Trim()}";
    }
}
