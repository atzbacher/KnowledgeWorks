using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models;

namespace LM.Review.Core.Services;

public sealed class ReviewWorkflowService : IReviewWorkflowService
{
    private readonly IReviewWorkflowStore _store;
    private readonly IReviewHookOrchestrator _hookOrchestrator;
    private readonly IReviewHookContextFactory _hookContextFactory;
    private readonly TimeProvider _timeProvider;

    public ReviewWorkflowService(
        IReviewWorkflowStore store,
        IReviewHookOrchestrator hookOrchestrator,
        IReviewHookContextFactory hookContextFactory,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
        _hookContextFactory = hookContextFactory ?? throw new ArgumentNullException(nameof(hookContextFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ReviewStage> CreateStageAsync(
        string projectId,
        string stageDefinitionId,
        IReadOnlyCollection<ReviewerAssignmentRequest> assignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageDefinitionId);
        ArgumentNullException.ThrowIfNull(assignments);

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException("At least one assignment must be provided when activating a stage.");
        }

        var project = await _store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Project '{projectId}' could not be located.");

        var definition = project.StageDefinitions.FirstOrDefault(
            d => string.Equals(d.Id, stageDefinitionId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Stage definition '{stageDefinitionId}' does not exist in project '{projectId}'.");

        ValidateAssignments(definition, assignments);

        var stageId = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var activatedAt = _timeProvider.GetUtcNow();
        var createdAssignments = new List<ScreeningAssignment>(assignments.Count);

        foreach (var request in assignments)
        {
            var assignment = ScreeningAssignment.Create(
                Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                stageId,
                request.ReviewerId,
                request.Role,
                ScreeningStatus.Pending,
                activatedAt);
            createdAssignments.Add(assignment);
        }

        var stage = ReviewStage.Create(
            stageId,
            project.Id,
            definition,
            createdAssignments,
            ConflictState.None,
            activatedAt);

        await _store.SaveStageAsync(stage, cancellationToken).ConfigureAwait(false);

        foreach (var assignment in createdAssignments)
        {
            await _store.SaveAssignmentAsync(project.Id, assignment, cancellationToken).ConfigureAwait(false);
            await PublishAssignmentUpdateAsync(stage, assignment, cancellationToken).ConfigureAwait(false);
        }

        return stage;
    }

    public async Task<ScreeningAssignment> SubmitDecisionAsync(
        string assignmentId,
        ScreeningStatus decision,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);

        if (decision is not ScreeningStatus.Included and not ScreeningStatus.Excluded)
        {
            throw new ArgumentException(
                $"Decision must be an inclusion or exclusion, not '{decision}'.",
                nameof(decision));
        }

        var existingAssignment = await _store.GetAssignmentAsync(assignmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Assignment '{assignmentId}' was not found.");

        var stage = await _store.GetStageAsync(existingAssignment.StageId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Stage '{existingAssignment.StageId}' was not found for assignment '{assignmentId}'.");

        var now = _timeProvider.GetUtcNow();
        var reviewerDecision = ReviewerDecision.Create(existingAssignment.Id, existingAssignment.ReviewerId, decision, now, notes);

        var updatedAssignment = ScreeningAssignment.Create(
            existingAssignment.Id,
            existingAssignment.StageId,
            existingAssignment.ReviewerId,
            existingAssignment.Role,
            reviewerDecision.Decision,
            existingAssignment.AssignedAt,
            now,
            reviewerDecision);

        await _store.SaveAssignmentAsync(stage.ProjectId, updatedAssignment, cancellationToken).ConfigureAwait(false);
        await PublishAssignmentUpdateAsync(stage, updatedAssignment, cancellationToken).ConfigureAwait(false);
        await PublishDecisionAsync(stage, updatedAssignment, reviewerDecision, cancellationToken).ConfigureAwait(false);

        var assignments = await _store.GetAssignmentsByStageAsync(stage.Id, cancellationToken).ConfigureAwait(false);
        if (assignments.Count == 0)
        {
            return updatedAssignment;
        }

        var previousState = stage.ConflictState;
        var newState = DetermineConflictState(stage, assignments);
        var completedAt = stage.CompletedAt;

        if (newState is ConflictState.Resolved or ConflictState.Escalated or ConflictState.Conflict)
        {
            completedAt = now;
        }

        if (previousState == newState && completedAt == stage.CompletedAt)
        {
            return updatedAssignment;
        }

        var updatedStage = ReviewStage.Create(
            stage.Id,
            stage.ProjectId,
            stage.Definition,
            assignments,
            newState,
            stage.ActivatedAt,
            completedAt,
            stage.Consensus);

        await _store.SaveStageAsync(updatedStage, cancellationToken).ConfigureAwait(false);
        await PublishStageTransitionAsync(updatedStage, previousState, newState, cancellationToken).ConfigureAwait(false);

        if (newState == ConflictState.Escalated)
        {
            var project = await _store.GetProjectAsync(stage.ProjectId, cancellationToken).ConfigureAwait(false);
            if (project is not null)
            {
                await EnsureEscalationStageAsync(project, assignments, cancellationToken).ConfigureAwait(false);
            }
        }

        return updatedAssignment;
    }

    private static void ValidateAssignments(
        StageDefinition definition,
        IReadOnlyCollection<ReviewerAssignmentRequest> assignments)
    {
        var requirement = definition.ReviewerRequirement;
        var totalRequired = requirement.TotalRequired;
        if (assignments.Count != totalRequired)
        {
            throw new InvalidOperationException(
                $"Stage '{definition.Name}' expects {totalRequired} reviewer assignment(s) but received {assignments.Count}.");
        }

        var providedCounts = assignments
            .GroupBy(a => a.Role)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var (role, requiredCount) in requirement.Requirements)
        {
            if (!providedCounts.TryGetValue(role, out var actual) || actual != requiredCount)
            {
                throw new InvalidOperationException(
                    $"Stage '{definition.Name}' expects {requiredCount} assignment(s) for role '{role}' but received {actual}.");
            }
        }

        var invalidRole = providedCounts.Keys.FirstOrDefault(role => !requirement.Requirements.ContainsKey(role));
        if (invalidRole != default)
        {
            throw new InvalidOperationException(
                $"Role '{invalidRole}' is not allowed for stage '{definition.Name}'.");
        }
    }

    private static ConflictState DetermineConflictState(ReviewStage stage, IReadOnlyList<ScreeningAssignment> assignments)
    {
        var totalRequired = stage.Definition.ReviewerRequirement.TotalRequired;
        var completedAssignments = assignments
            .Where(a => a.Status is ScreeningStatus.Included or ScreeningStatus.Excluded)
            .ToList();

        if (completedAssignments.Count < totalRequired)
        {
            return stage.ConflictState;
        }

        var firstDecision = completedAssignments[0].Status;
        var unanimous = completedAssignments.All(a => a.Status == firstDecision);

        if (unanimous)
        {
            return ConflictState.Resolved;
        }

        return stage.Definition.ConsensusPolicy.EscalateOnDisagreement
            ? ConflictState.Escalated
            : ConflictState.Conflict;
    }

    private async Task EnsureEscalationStageAsync(
        ReviewProject project,
        IReadOnlyList<ScreeningAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var existingStages = await _store
            .GetStagesByProjectAsync(project.Id, cancellationToken)
            .ConfigureAwait(false);

        var usedDefinitionIds = new HashSet<string>(existingStages.Select(stage => stage.Definition.Id), StringComparer.Ordinal);

        StageDefinition? SelectNextDefinition(ReviewStageType stageType)
        {
            return project.StageDefinitions.FirstOrDefault(definition =>
                definition.StageType == stageType && !usedDefinitionIds.Contains(definition.Id));
        }

        var targetDefinition = SelectNextDefinition(ReviewStageType.ConsensusMeeting)
            ?? SelectNextDefinition(ReviewStageType.QualityAssurance);

        if (targetDefinition is null)
        {
            return;
        }

        var stageId = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var activatedAt = _timeProvider.GetUtcNow();
        var assignmentLookup = assignments
            .GroupBy(a => a.Role)
            .ToDictionary(group => group.Key, group => group.Select(a => a.ReviewerId).ToList());

        var reassigned = new List<ScreeningAssignment>();
        foreach (var (role, count) in targetDefinition.ReviewerRequirement.Requirements)
        {
            if (!assignmentLookup.TryGetValue(role, out var reviewers) || reviewers.Count < count)
            {
                // Cannot satisfy the reviewer requirement for the escalation stage.
                return;
            }

            for (var index = 0; index < count; index++)
            {
                var reviewerId = reviewers[index];
                reassigned.Add(ScreeningAssignment.Create(
                    Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                    stageId,
                    reviewerId,
                    role,
                    ScreeningStatus.Pending,
                    activatedAt));
            }
        }

        if (reassigned.Count == 0)
        {
            return;
        }

        var escalationStage = ReviewStage.Create(
            stageId,
            project.Id,
            targetDefinition,
            reassigned,
            ConflictState.None,
            activatedAt);

        await _store.SaveStageAsync(escalationStage, cancellationToken).ConfigureAwait(false);
        foreach (var assignment in reassigned)
        {
            await _store.SaveAssignmentAsync(project.Id, assignment, cancellationToken).ConfigureAwait(false);
            await PublishAssignmentUpdateAsync(escalationStage, assignment, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishAssignmentUpdateAsync(ReviewStage stage, ScreeningAssignment assignment, CancellationToken cancellationToken)
    {
        var context = _hookContextFactory.CreateAssignmentUpdated(stage, assignment);
        await _hookOrchestrator.ProcessAsync(stage.ProjectId, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishDecisionAsync(ReviewStage stage, ScreeningAssignment assignment, ReviewerDecision decision, CancellationToken cancellationToken)
    {
        var context = _hookContextFactory.CreateReviewerDecisionRecorded(assignment, decision);
        await _hookOrchestrator.ProcessAsync(stage.ProjectId, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishStageTransitionAsync(ReviewStage stage, ConflictState previousState, ConflictState currentState, CancellationToken cancellationToken)
    {
        var context = _hookContextFactory.CreateStageTransition(stage, previousState, currentState);
        await _hookOrchestrator.ProcessAsync(stage.ProjectId, context, cancellationToken).ConfigureAwait(false);
    }
}
