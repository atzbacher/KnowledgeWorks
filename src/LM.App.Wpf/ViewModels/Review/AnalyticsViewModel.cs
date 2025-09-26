using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Analytics;
using LM.Review.Core.Services;

namespace LM.App.Wpf.ViewModels.Review
{
    internal sealed class AnalyticsViewModel : ViewModelBase
    {
        private readonly IReviewAnalyticsService _analyticsService;
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private ProjectAnalyticsSnapshot? _projectSnapshot;
        private ReviewStage? _stage;
        private StageAnalyticsSummary? _stageSummary;

        public AnalyticsViewModel(
            IReviewAnalyticsService analyticsService,
            HookOrchestrator hookOrchestrator,
            IUserContext userContext)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        public ProjectAnalyticsSnapshot? ProjectSnapshot
        {
            get => _projectSnapshot;
            private set => SetProperty(ref _projectSnapshot, value);
        }

        public ReviewStage? Stage
        {
            get => _stage;
            private set => SetProperty(ref _stage, value);
        }

        public StageAnalyticsSummary? StageSummary
        {
            get => _stageSummary;
            private set => SetProperty(ref _stageSummary, value);
        }

        public async Task UpdateAsync(
            ReviewProject project,
            IReadOnlyList<ReviewStage> stages,
            ReviewStage? focusStage,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(stages);

            ProjectSnapshot = _analyticsService.CreateSnapshot(project, stages);
            Stage = focusStage;
            StageSummary = focusStage is null ? null : StageAnalyticsSummary.Create(focusStage);

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                project.Id,
                _userContext.UserName,
                "review.ui.analytics.updated",
                BuildTags(project, focusStage, stages.Count),
                cancellationToken).ConfigureAwait(false);
        }

        private static IEnumerable<string> BuildTags(ReviewProject project, ReviewStage? stage, int stageCount)
        {
            yield return $"projectId:{project.Id}";
            yield return $"stageCount:{stageCount}";
            if (stage is not null)
            {
                yield return $"stageId:{stage.Id}";
                yield return $"conflictState:{stage.ConflictState}";
                yield return $"assignmentCount:{stage.Assignments.Count}";
            }
        }

        internal sealed record StageAnalyticsSummary(int Included, int Excluded, int Pending)
        {
            public static StageAnalyticsSummary Create(ReviewStage stage)
            {
                var included = stage.Assignments.Count(a => a.Status == ScreeningStatus.Included);
                var excluded = stage.Assignments.Count(a => a.Status == ScreeningStatus.Excluded);
                var pending = stage.Assignments.Count(a => a.Status == ScreeningStatus.Pending);
                return new StageAnalyticsSummary(included, excluded, pending);
            }
        }
    }
}
