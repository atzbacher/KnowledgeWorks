using System;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review
{
    internal sealed class QualityAssuranceViewModel : ViewModelBase
    {
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private ReviewStage? _stage;
        private bool _requiresConsensus;
        private ConflictState _conflictState;
        private DateTimeOffset? _completedAtUtc;

        public QualityAssuranceViewModel(HookOrchestrator hookOrchestrator, IUserContext userContext)
        {
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        public ReviewStage? Stage
        {
            get => _stage;
            private set => SetProperty(ref _stage, value);
        }

        public bool RequiresConsensus
        {
            get => _requiresConsensus;
            private set => SetProperty(ref _requiresConsensus, value);
        }

        public ConflictState ConflictState
        {
            get => _conflictState;
            private set => SetProperty(ref _conflictState, value);
        }

        public DateTimeOffset? CompletedAtUtc
        {
            get => _completedAtUtc;
            private set => SetProperty(ref _completedAtUtc, value);
        }

        public async Task InitializeAsync(ReviewStage stage, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stage);

            Stage = stage;
            ConflictState = stage.ConflictState;
            RequiresConsensus = stage.ConflictState is ConflictState.Conflict or ConflictState.Escalated;
            CompletedAtUtc = stage.CompletedAt;

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                stage.Id,
                _userContext.UserName,
                "review.ui.qa.evaluated",
                new[]
                {
                    $"stageId:{stage.Id}",
                    $"state:{stage.ConflictState}",
                    $"requiresConsensus:{RequiresConsensus}"
                },
                cancellationToken).ConfigureAwait(false);
        }

        public void Reset()
        {
            Stage = null;
            ConflictState = ConflictState.None;
            RequiresConsensus = false;
            CompletedAtUtc = null;
        }
    }
}
