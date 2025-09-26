#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models.Analytics;
using LM.Review.Core.Services;

namespace LM.App.Wpf.ViewModels.Review;

internal sealed class ReviewDashboardViewModel
{
    private readonly IReviewWorkflowStore _store;
    private readonly IReviewAnalyticsService _analytics;

    public ReviewDashboardViewModel(IReviewWorkflowStore store, IReviewAnalyticsService analytics)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
    }

    public async Task<ProjectAnalyticsSnapshot?> LoadProjectAnalyticsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var project = await _store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return null;
        }

        var stages = await _store.GetStagesByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return _analytics.CreateSnapshot(project, stages);
    }
}
