using System;
using System.Collections.Generic;
using LM.Core.Utils;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Review;

public sealed class ReviewHookContextFactory : IReviewHookContextFactory
{
    public IReviewHookContext CreateProjectCreated(ReviewProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var tags = new List<string>
        {
            $"projectId:{project.Id}".Trim(),
            $"createdAt:{project.CreatedAt:O}"
        };

        return BuildContext(CreateEvent("review.project.created", tags));
    }

    public IReviewHookContext CreateAssignmentUpdated(ReviewStage stage, ScreeningAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(assignment);

        var tags = new List<string>
        {
            $"projectId:{stage.ProjectId}",
            $"stageId:{stage.Id}",
            $"assignmentId:{assignment.Id}",
            $"status:{assignment.Status}",
            $"role:{assignment.Role}",
            $"reviewer:{assignment.ReviewerId}"
        };

        return BuildContext(CreateEvent("review.assignment.updated", tags));
    }

    public IReviewHookContext CreateReviewerDecisionRecorded(ScreeningAssignment assignment, ReviewerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(decision);

        var tags = new List<string>
        {
            $"stageId:{assignment.StageId}",
            $"assignmentId:{assignment.Id}",
            $"decision:{decision.Decision}",
            $"decidedAt:{decision.DecidedAt:O}",
            $"reviewer:{decision.ReviewerId}"
        };

        return BuildContext(CreateEvent("review.assignment.decision", tags));
    }

    public IReviewHookContext CreateConsensusResolved(ReviewStage stage, ConsensusOutcome consensus)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(consensus);

        var tags = new List<string>
        {
            $"projectId:{stage.ProjectId}",
            $"stageId:{stage.Id}",
            $"approved:{consensus.Approved}",
            $"result:{consensus.ResultingState}",
            $"resolvedAt:{consensus.ResolvedAt:O}"
        };

        if (!string.IsNullOrWhiteSpace(consensus.ResolvedBy))
        {
            tags.Add($"resolvedBy:{consensus.ResolvedBy}");
        }

        return BuildContext(CreateEvent("review.consensus.recorded", tags));
    }

    public IReviewHookContext CreateStageTransition(ReviewStage stage, ConflictState previousState, ConflictState currentState)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var tags = new List<string>
        {
            $"projectId:{stage.ProjectId}",
            $"stageId:{stage.Id}",
            $"from:{previousState}",
            $"to:{currentState}",
            $"activatedAt:{stage.ActivatedAt:O}"
        };

        if (stage.CompletedAt is { } completedAt)
        {
            tags.Add($"completedAt:{completedAt:O}");
        }

        return BuildContext(CreateEvent("review.stage.transition", tags));
    }

    private static HookContext BuildContext(HookM.EntryChangeLogEvent changeEvent)
    {
        var hook = new HookM.EntryChangeLogHook
        {
            Events = new List<HookM.EntryChangeLogEvent> { changeEvent }
        };

        return new HookContext
        {
            ChangeLog = hook
        };
    }

    private static HookM.EntryChangeLogEvent CreateEvent(string action, IEnumerable<string> tags)
    {
        var userName = SystemUser.GetCurrent();
        var sanitizedTags = new List<string>();
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                sanitizedTags.Add(tag);
            }
        }

        var details = new HookM.ChangeLogAttachmentDetails
        {
            Tags = sanitizedTags
        };

        return new HookM.EntryChangeLogEvent
        {
            Action = action,
            PerformedBy = userName,
            TimestampUtc = DateTime.UtcNow,
            Details = details
        };
    }
}
