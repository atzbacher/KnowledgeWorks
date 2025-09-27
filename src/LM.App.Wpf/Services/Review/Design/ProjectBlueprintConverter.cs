#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LM.Review.Core.Models;

namespace LM.App.Wpf.Services.Review.Design;

internal static class ProjectBlueprintConverter
{
    public static ReviewProject ToReviewProject(ProjectBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        var stages = blueprint.Stages
            .Select(ToStageDefinition)
            .ToList();

        if (stages.Count == 0)
        {
            throw new InvalidOperationException("At least one stage must be defined before creating a review project.");
        }

        var auditEntry = ReviewAuditTrail.AuditEntry.Create(
            $"audit-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}",
            blueprint.CreatedBy,
            "project.created",
            blueprint.CreatedAtUtc,
            $"litsearch:{blueprint.LitSearchEntryId}:run:{blueprint.LitSearchRunId}");

        var auditTrail = ReviewAuditTrail.Create(new[] { auditEntry });

        return ReviewProject.Create(
            blueprint.ProjectId,
            blueprint.Name,
            blueprint.CreatedAtUtc,
            stages,
            auditTrail);
    }

    private static StageDefinition ToStageDefinition(StageBlueprint stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var requirements = new List<KeyValuePair<ReviewerRole, int>>();
        if (stage.PrimaryReviewers > 0)
        {
            requirements.Add(new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, stage.PrimaryReviewers));
        }

        if (stage.SecondaryReviewers > 0)
        {
            requirements.Add(new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, stage.SecondaryReviewers));
        }

        if (requirements.Count == 0)
        {
            throw new InvalidOperationException($"Stage '{stage.Name}' must define at least one reviewer.");
        }

        var reviewerRequirement = ReviewerRequirement.Create(requirements);
        var consensus = ResolveConsensusPolicy(stage, reviewerRequirement);

        return StageDefinition.Create(
            stage.StageId,
            stage.Name,
            stage.StageType,
            reviewerRequirement,
            consensus);
    }

    private static StageConsensusPolicy ResolveConsensusPolicy(StageBlueprint stage, ReviewerRequirement requirement)
    {
        if (!stage.RequiresConsensus)
        {
            return StageConsensusPolicy.Disabled();
        }

        var totalRequired = requirement.TotalRequired;
        if (totalRequired <= 0)
        {
            return StageConsensusPolicy.Disabled();
        }

        var minimumAgreements = stage.MinimumAgreements <= 0
            ? totalRequired
            : Math.Min(stage.MinimumAgreements, totalRequired);

        return StageConsensusPolicy.RequireAgreement(minimumAgreements, stage.EscalateOnDisagreement, null);
    }
}
