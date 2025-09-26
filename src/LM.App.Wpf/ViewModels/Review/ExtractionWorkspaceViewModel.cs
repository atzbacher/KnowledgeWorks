using System;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review
{
    internal sealed class ExtractionWorkspaceViewModel : ViewModelBase
    {
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private ReviewStage? _stage;
        private bool _isActive;
        private DateTimeOffset? _lastEnteredUtc;

        public ExtractionWorkspaceViewModel(HookOrchestrator hookOrchestrator, IUserContext userContext)
        {
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        public ReviewStage? Stage
        {
            get => _stage;
            private set => SetProperty(ref _stage, value);
        }

        public bool IsActive
        {
            get => _isActive;
            private set => SetProperty(ref _isActive, value);
        }

        public DateTimeOffset? LastEnteredUtc
        {
            get => _lastEnteredUtc;
            private set => SetProperty(ref _lastEnteredUtc, value);
        }

        public async Task InitializeAsync(ReviewStage stage, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stage);

            Stage = stage;
            IsActive = true;
            LastEnteredUtc = DateTimeOffset.UtcNow;

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                stage.Id,
                _userContext.UserName,
                "review.ui.extraction.entered",
                new[]
                {
                    $"stageId:{stage.Id}",
                    $"projectId:{stage.ProjectId}",
                    $"state:{stage.ConflictState}"
                },
                cancellationToken).ConfigureAwait(false);
        }

        public void Reset()
        {
            Stage = null;
            IsActive = false;
            LastEnteredUtc = null;
        }
    }
}
