#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Review.Core.Models;
using LM.Review.Core.Services;

namespace LM.Infrastructure.Review;

/// <summary>
/// Persists review workflow state in the workspace by delegating to <see cref="JsonReviewProjectStore"/>.
/// </summary>
public sealed class WorkspaceReviewWorkflowStore : IReviewWorkflowStore
{
    private readonly JsonReviewProjectStore _store;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public WorkspaceReviewWorkflowStore(IWorkSpaceService workspace)
    {
        _store = new JsonReviewProjectStore(workspace ?? throw new ArgumentNullException(nameof(workspace)));
    }

    public async Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReviewProject>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetStageAsync(stageId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetStagesByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetAssignmentAsync(assignmentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetAssignmentsByStageAsync(stageId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _store.SaveStageAsync(stage, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _store.SaveAssignmentAsync(projectId, assignment, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
