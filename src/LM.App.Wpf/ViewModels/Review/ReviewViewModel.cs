using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.App.Wpf.Services.Review;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;

namespace LM.App.Wpf.ViewModels.Review
{
    internal sealed class ReviewViewModel : ViewModelBase
    {
        private readonly IReviewWorkflowStore _store;
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private readonly ProjectDashboardViewModel _projectDashboard;
        private readonly ScreeningQueueViewModel _screeningQueue;
        private readonly AssignmentDetailViewModel _assignmentDetail;
        private readonly ExtractionWorkspaceViewModel _extractionWorkspace;
        private readonly QualityAssuranceViewModel _qualityAssurance;
        private readonly AnalyticsViewModel _analyticsViewModel;
        private readonly IReviewProjectLauncher _projectLauncher;
        private IReadOnlyList<ReviewProject> _projects = Array.Empty<ReviewProject>();
        private IReadOnlyList<ReviewStage> _stages = Array.Empty<ReviewStage>();
        private IReadOnlyList<ScreeningAssignment> _assignments = Array.Empty<ScreeningAssignment>();
        private ReviewProject? _selectedProject;
        private ReviewStage? _selectedStage;
        private bool _isBusy;

        public ReviewViewModel(
            IReviewWorkflowStore store,
            HookOrchestrator hookOrchestrator,
            IUserContext userContext,
            ProjectDashboardViewModel projectDashboard,
            ScreeningQueueViewModel screeningQueue,
            AssignmentDetailViewModel assignmentDetail,
            ExtractionWorkspaceViewModel extractionWorkspace,
            QualityAssuranceViewModel qualityAssurance,
            AnalyticsViewModel analyticsViewModel,
            IReviewProjectLauncher projectLauncher)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _projectDashboard = projectDashboard ?? throw new ArgumentNullException(nameof(projectDashboard));
            _screeningQueue = screeningQueue ?? throw new ArgumentNullException(nameof(screeningQueue));
            _assignmentDetail = assignmentDetail ?? throw new ArgumentNullException(nameof(assignmentDetail));
            _extractionWorkspace = extractionWorkspace ?? throw new ArgumentNullException(nameof(extractionWorkspace));
            _qualityAssurance = qualityAssurance ?? throw new ArgumentNullException(nameof(qualityAssurance));
            _analyticsViewModel = analyticsViewModel ?? throw new ArgumentNullException(nameof(analyticsViewModel));
            _projectLauncher = projectLauncher ?? throw new ArgumentNullException(nameof(projectLauncher));

            SelectProjectCommand = new AsyncRelayCommand(SelectProjectCommandAsync, CanSelectProject);
            NavigateStageCommand = new AsyncRelayCommand(NavigateStageCommandAsync, CanNavigateStage);
            RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), CanRefresh);
            CreateProjectCommand = new AsyncRelayCommand(() => CreateProjectAsync(CancellationToken.None), CanInitiateProjectAction);
            LoadProjectCommand = new AsyncRelayCommand(() => LoadProjectAsync(CancellationToken.None), CanInitiateProjectAction);
        }

        public IReadOnlyList<ReviewProject> Projects
        {
            get => _projects;
            private set => SetProperty(ref _projects, value);
        }

        public IReadOnlyList<ReviewStage> Stages
        {
            get => _stages;
            private set => SetProperty(ref _stages, value);
        }

        public IReadOnlyList<ScreeningAssignment> Assignments
        {
            get => _assignments;
            private set => SetProperty(ref _assignments, value);
        }

        public ReviewProject? SelectedProject
        {
            get => _selectedProject;
            private set
            {
                if (SetProperty(ref _selectedProject, value))
                {
                    SelectProjectCommand.RaiseCanExecuteChanged();
                    RefreshCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ReviewStage? SelectedStage
        {
            get => _selectedStage;
            private set
            {
                if (SetProperty(ref _selectedStage, value))
                {
                    NavigateStageCommand.RaiseCanExecuteChanged();
                    RefreshCommand.RaiseCanExecuteChanged();
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
                    SelectProjectCommand.RaiseCanExecuteChanged();
                    NavigateStageCommand.RaiseCanExecuteChanged();
                    RefreshCommand.RaiseCanExecuteChanged();
                    CreateProjectCommand.RaiseCanExecuteChanged();
                    LoadProjectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ProjectDashboardViewModel Dashboard => _projectDashboard;

        public ScreeningQueueViewModel ScreeningQueue => _screeningQueue;

        public AssignmentDetailViewModel AssignmentDetail => _assignmentDetail;

        public ExtractionWorkspaceViewModel ExtractionWorkspace => _extractionWorkspace;

        public QualityAssuranceViewModel QualityAssurance => _qualityAssurance;

        public AnalyticsViewModel Analytics => _analyticsViewModel;

        public IAsyncRelayCommand SelectProjectCommand { get; }

        public IAsyncRelayCommand NavigateStageCommand { get; }

        public IAsyncRelayCommand RefreshCommand { get; }

        public IAsyncRelayCommand CreateProjectCommand { get; }

        public IAsyncRelayCommand LoadProjectCommand { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            IsBusy = true;
            try
            {
                Projects = await _store.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
                Stages = Array.Empty<ReviewStage>();
                Assignments = Array.Empty<ScreeningAssignment>();
                SelectedProject = null;
                SelectedStage = null;
                await _assignmentDetail.SelectAssignmentAsync(null, cancellationToken).ConfigureAwait(false);
                _extractionWorkspace.Reset();
                _qualityAssurance.Reset();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task SelectProjectAsync(string projectId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
            return RunExclusiveAsync(ct => SelectProjectCoreAsync(projectId, ct), cancellationToken);
        }

        public Task NavigateStageAsync(string stageId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
            return RunExclusiveAsync(ct => NavigateStageCoreAsync(stageId, ct), cancellationToken);
        }

        private async Task SelectProjectCommandAsync(object? parameter)
        {
            if (parameter is not string projectId)
            {
                return;
            }

            await SelectProjectAsync(projectId, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task NavigateStageCommandAsync(object? parameter)
        {
            if (parameter is not string stageId)
            {
                return;
            }

            await NavigateStageAsync(stageId, CancellationToken.None).ConfigureAwait(false);
        }

        private bool CanSelectProject(object? parameter)
        {
            return !IsBusy && parameter is string id && !string.IsNullOrWhiteSpace(id);
        }

        private bool CanNavigateStage(object? parameter)
        {
            return !IsBusy && SelectedProject is not null && parameter is string id && !string.IsNullOrWhiteSpace(id);
        }

        private bool CanRefresh()
        {
            return !IsBusy && SelectedProject is not null;
        }

        private bool CanInitiateProjectAction()
        {
            return !IsBusy;
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            if (SelectedProject is null)
            {
                return Task.CompletedTask;
            }

            return RunExclusiveAsync(ct => SelectProjectCoreAsync(SelectedProject.Id, ct, SelectedStage?.Id), cancellationToken);
        }

        public Task CreateProjectAsync(CancellationToken cancellationToken)
        {
            return RunExclusiveAsync(CreateProjectCoreAsync, cancellationToken);
        }

        public Task LoadProjectAsync(CancellationToken cancellationToken)
        {
            return RunExclusiveAsync(LoadProjectCoreAsync, cancellationToken);
        }

        private async Task CreateProjectCoreAsync(CancellationToken cancellationToken)
        {
            var project = await _projectLauncher.CreateProjectAsync(cancellationToken).ConfigureAwait(false);
            if (project is null)
            {
                return;
            }

            Projects = await _store.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
            await SelectProjectCoreAsync(project.Id, cancellationToken).ConfigureAwait(false);
        }

        private async Task LoadProjectCoreAsync(CancellationToken cancellationToken)
        {
            var project = await _projectLauncher.LoadProjectAsync(cancellationToken).ConfigureAwait(false);
            if (project is null)
            {
                return;
            }

            Projects = await _store.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
            await SelectProjectCoreAsync(project.Id, cancellationToken).ConfigureAwait(false);
        }

        private async Task SelectProjectCoreAsync(string projectId, CancellationToken cancellationToken, string? stageToMaintain = null)
        {
            var project = await _store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Project '{projectId}' could not be located.");

            var stages = await _store.GetStagesByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
            var selectedStage = stageToMaintain is null
                ? stages.FirstOrDefault()
                : stages.FirstOrDefault(stage => string.Equals(stage.Id, stageToMaintain, StringComparison.Ordinal));

            SelectedProject = project;
            Stages = stages;
            await _projectDashboard.InitializeAsync(project, stages, cancellationToken).ConfigureAwait(false);
            await _analyticsViewModel.UpdateAsync(project, stages, selectedStage, cancellationToken).ConfigureAwait(false);

            if (selectedStage is not null)
            {
                await NavigateStageCoreAsync(selectedStage.Id, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                SelectedStage = null;
                Assignments = Array.Empty<ScreeningAssignment>();
                await _assignmentDetail.SelectAssignmentAsync(null, cancellationToken).ConfigureAwait(false);
                _extractionWorkspace.Reset();
                _qualityAssurance.Reset();
            }

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                project.Id,
                _userContext.UserName,
                "review.ui.project.selected",
                new[]
                {
                    $"projectId:{project.Id}",
                    $"stageCount:{stages.Count}"
                },
                cancellationToken).ConfigureAwait(false);
        }

        private async Task NavigateStageCoreAsync(string stageId, CancellationToken cancellationToken)
        {
            var stage = Stages.FirstOrDefault(s => string.Equals(s.Id, stageId, StringComparison.Ordinal))
                ?? await _store.GetStageAsync(stageId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Stage '{stageId}' could not be found.");

            var assignments = await _store.GetAssignmentsByStageAsync(stage.Id, cancellationToken).ConfigureAwait(false);
            SelectedStage = stage;
            Assignments = assignments;

            await _screeningQueue.LoadAsync(stage, assignments, cancellationToken).ConfigureAwait(false);
            var initialAssignment = _screeningQueue.FindNextPendingAssignment() ?? assignments.FirstOrDefault();
            await _assignmentDetail.SelectAssignmentAsync(initialAssignment, cancellationToken).ConfigureAwait(false);
            await _extractionWorkspace.InitializeAsync(stage, cancellationToken).ConfigureAwait(false);
            await _qualityAssurance.InitializeAsync(stage, cancellationToken).ConfigureAwait(false);

            if (SelectedProject is not null)
            {
                await _analyticsViewModel.UpdateAsync(SelectedProject, Stages, stage, cancellationToken).ConfigureAwait(false);
            }

            await ReviewChangeLogWriter.WriteAsync(
                _hookOrchestrator,
                stage.Id,
                _userContext.UserName,
                "review.ui.stage.navigated",
                new[]
                {
                    $"stageId:{stage.Id}",
                    $"projectId:{stage.ProjectId}",
                    $"assignmentCount:{assignments.Count}"
                },
                cancellationToken).ConfigureAwait(false);
        }

        private Task RunExclusiveAsync(Func<CancellationToken, Task> callback, CancellationToken cancellationToken)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (IsBusy)
            {
                return Task.CompletedTask;
            }

            return ExecuteExclusiveAsync(callback, cancellationToken);
        }

        private async Task ExecuteExclusiveAsync(Func<CancellationToken, Task> callback, CancellationToken cancellationToken)
        {
            IsBusy = true;
            try
            {
                await callback(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

}
