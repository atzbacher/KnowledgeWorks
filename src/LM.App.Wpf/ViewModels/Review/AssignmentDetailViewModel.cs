using System;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;

namespace LM.App.Wpf.ViewModels.Review
{
    internal sealed class AssignmentDetailViewModel : ViewModelBase
    {
        private readonly IReviewWorkflowService _workflowService;
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private ScreeningAssignment? _assignment;
        private bool _isBusy;
        private string? _notes;

        public AssignmentDetailViewModel(
            IReviewWorkflowService workflowService,
            HookOrchestrator hookOrchestrator,
            IUserContext userContext)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

            SubmitIncludeCommand = new AsyncRelayCommand(
                _ => SubmitDecisionAsync(ScreeningStatus.Included, CancellationToken.None),
                _ => CanSubmitDecision());

            SubmitExcludeCommand = new AsyncRelayCommand(
                _ => SubmitDecisionAsync(ScreeningStatus.Excluded, CancellationToken.None),
                _ => CanSubmitDecision());
        }

        public ScreeningAssignment? SelectedAssignment
        {
            get => _assignment;
            private set
            {
                if (SetProperty(ref _assignment, value))
                {
                    SubmitIncludeCommand.RaiseCanExecuteChanged();
                    SubmitExcludeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    SubmitIncludeCommand.RaiseCanExecuteChanged();
                    SubmitExcludeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public IAsyncRelayCommand SubmitIncludeCommand { get; }

        public IAsyncRelayCommand SubmitExcludeCommand { get; }

        public async Task SelectAssignmentAsync(ScreeningAssignment? assignment, CancellationToken cancellationToken)
        {
            SelectedAssignment = assignment;
            if (assignment is null)
            {
                return;
            }

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                assignment.StageId,
                _userContext.UserName,
                "review.ui.assignment.selected",
                new[]
                {
                    $"assignmentId:{assignment.Id}",
                    $"reviewer:{assignment.ReviewerId}",
                    $"status:{assignment.Status}"
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task SubmitDecisionAsync(ScreeningStatus status, CancellationToken cancellationToken)
        {
            if (SelectedAssignment is null)
            {
                throw new InvalidOperationException("An assignment must be selected before submitting a decision.");
            }

            if (status is not ScreeningStatus.Included and not ScreeningStatus.Excluded)
            {
                throw new ArgumentException("A decision must be an inclusion or exclusion.", nameof(status));
            }

            IsBusy = true;
            try
            {
                var updated = await _workflowService
                    .SubmitDecisionAsync(SelectedAssignment.Id, status, Notes, cancellationToken)
                    .ConfigureAwait(false);

                SelectedAssignment = updated;

                await ReviewChangeLogWriter.WriteAsync(
                    _hookOrchestrator,
                    updated.StageId,
                    _userContext.UserName,
                    "review.ui.assignment.decision",
                    new[]
                    {
                        $"assignmentId:{updated.Id}",
                        $"decision:{updated.Status}",
                        $"reviewer:{updated.ReviewerId}"
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSubmitDecision()
        {
            return !IsBusy && SelectedAssignment is not null;
        }
    }
}
