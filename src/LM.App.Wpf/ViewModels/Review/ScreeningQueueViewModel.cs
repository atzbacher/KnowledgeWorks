using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review
{
    internal sealed class ScreeningQueueViewModel : ViewModelBase
    {
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private ReviewStage? _stage;
        private IReadOnlyList<ScreeningAssignment> _assignments = Array.Empty<ScreeningAssignment>();
        private DateTimeOffset? _lastRefreshedUtc;
        private bool _isLoading;

        public ScreeningQueueViewModel(HookOrchestrator hookOrchestrator, IUserContext userContext)
        {
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        public ReviewStage? Stage
        {
            get => _stage;
            private set => SetProperty(ref _stage, value);
        }

        public IReadOnlyList<ScreeningAssignment> Assignments
        {
            get => _assignments;
            private set => SetProperty(ref _assignments, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public DateTimeOffset? LastRefreshedUtc
        {
            get => _lastRefreshedUtc;
            private set => SetProperty(ref _lastRefreshedUtc, value);
        }

        public async Task LoadAsync(
            ReviewStage stage,
            IReadOnlyList<ScreeningAssignment> assignments,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stage);
            ArgumentNullException.ThrowIfNull(assignments);

            IsLoading = true;
            try
            {
                Stage = stage;
                Assignments = assignments;
                LastRefreshedUtc = DateTimeOffset.UtcNow;

                var tags = new List<string>
                {
                    $"stageId:{stage.Id}",
                    $"projectId:{stage.ProjectId}",
                    $"assignmentCount:{assignments.Count}",
                    $"status:{stage.ConflictState}"
                };

                await ReviewChangeLogWriter.WriteAsync(
                    _hookOrchestrator,
                    stage.Id,
                    _userContext.UserName,
                    "review.ui.queue.loaded",
                    tags,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public ScreeningAssignment? FindNextPendingAssignment()
        {
            return Assignments.FirstOrDefault(a => a.Status == ScreeningStatus.Pending);
        }
    }
}
