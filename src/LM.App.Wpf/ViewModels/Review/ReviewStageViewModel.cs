#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;

namespace LM.App.Wpf.ViewModels.Review;

internal sealed class ReviewStageViewModel
{
    private readonly IReviewWorkflowStore _store;
    private readonly IReviewWorkflowService _workflowService;

    public ReviewStageViewModel(IReviewWorkflowStore store, IReviewWorkflowService workflowService)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
    }

    public Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        return _store.GetStageAsync(stageId, cancellationToken);
    }

    public Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsAsync(string stageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        return _store.GetAssignmentsByStageAsync(stageId, cancellationToken);
    }

    public Task<ScreeningAssignment> SubmitDecisionAsync(string assignmentId, ScreeningStatus decision, string? notes, CancellationToken cancellationToken = default)
        => _workflowService.SubmitDecisionAsync(assignmentId, decision, notes, cancellationToken);
}
