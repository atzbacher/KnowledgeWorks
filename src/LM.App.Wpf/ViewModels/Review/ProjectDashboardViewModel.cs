using System;
using System.Collections.Generic;
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
    internal sealed class ProjectDashboardViewModel : ViewModelBase
    {
        private readonly IReviewAnalyticsService _analyticsService;
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private ProjectAnalyticsSnapshot? _snapshot;
        private ReviewProject? _project;

        public ProjectDashboardViewModel(
            IReviewAnalyticsService analyticsService,
            HookOrchestrator hookOrchestrator,
            IUserContext userContext)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        public ReviewProject? Project
        {
            get => _project;
            private set => SetProperty(ref _project, value);
        }

        public ProjectAnalyticsSnapshot? Snapshot
        {
            get => _snapshot;
            private set => SetProperty(ref _snapshot, value);
        }

        public async Task InitializeAsync(
            ReviewProject project,
            IReadOnlyList<ReviewStage> stages,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(stages);

            Project = project;
            Snapshot = _analyticsService.CreateSnapshot(project, stages);

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                project.Id,
                _userContext.UserName,
                "review.ui.dashboard.loaded",
                new[]
                {
                    $"projectId:{project.Id}",
                    $"stageCount:{stages.Count}"
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
