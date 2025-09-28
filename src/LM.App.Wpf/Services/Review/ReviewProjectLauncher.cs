#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Services;
using LM.App.Wpf.Services.Review.Design;
using LM.App.Wpf.ViewModels.Review;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;

namespace LM.App.Wpf.Services.Review
{
    internal sealed class ReviewProjectLauncher : IReviewProjectLauncher
    {
        private readonly IDialogService _dialogService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IEntryStore _entryStore;
        private readonly IReviewWorkflowStore _workflowStore;
        private readonly IReviewHookContextFactory _hookContextFactory;
        private readonly IReviewHookOrchestrator _reviewHookOrchestrator;
        private readonly HookOrchestrator _changeLogOrchestrator;
        private readonly IUserContext _userContext;
        private readonly IWorkSpaceService _workspace;
        private readonly ILitSearchRunPicker _runPicker;
        private readonly IReviewCreationDiagnostics _diagnostics;
        private readonly Func<ProjectEditorViewModel> _projectEditorFactory;


        public ReviewProjectLauncher(
            IDialogService dialogService,
            IEntryStore entryStore,
            IReviewWorkflowStore workflowStore,
            IReviewHookContextFactory hookContextFactory,
            IReviewHookOrchestrator reviewHookOrchestrator,
            HookOrchestrator changeLogOrchestrator,
            IUserContext userContext,
            IWorkSpaceService workspace,
            ILitSearchRunPicker runPicker,
            IMessageBoxService messageBoxService,
            IReviewCreationDiagnostics diagnostics,
            Func<ProjectEditorViewModel> projectEditorFactory)

        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _entryStore = entryStore ?? throw new ArgumentNullException(nameof(entryStore));
            _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
            _hookContextFactory = hookContextFactory ?? throw new ArgumentNullException(nameof(hookContextFactory));
            _reviewHookOrchestrator = reviewHookOrchestrator ?? throw new ArgumentNullException(nameof(reviewHookOrchestrator));
            _changeLogOrchestrator = changeLogOrchestrator ?? throw new ArgumentNullException(nameof(changeLogOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _runPicker = runPicker ?? throw new ArgumentNullException(nameof(runPicker));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _projectEditorFactory = projectEditorFactory ?? throw new ArgumentNullException(nameof(projectEditorFactory));
        }

        public async Task<ReviewProject?> CreateProjectAsync(CancellationToken cancellationToken)
        {
            try
            {
                _diagnostics.RecordStep("Starting review project creation flow.");
                var selection = await _runPicker.PickAsync(cancellationToken);
                if (selection is null)

                {
                    _diagnostics.RecordStep("Review project creation cancelled before selecting a run.");
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();

                _diagnostics.RecordStep($"Selected LitSearch run '{selection.RunId}' from entry '{selection.EntryId}'.");

                var entry = await _entryStore.GetByIdAsync(selection.EntryId, cancellationToken);
                if (entry is null)
                {
                    _diagnostics.RecordStep($"No entry metadata loaded for '{selection.EntryId}'. Proceeding with defaults.");
                }

                var checkedEntryIds = selection.CheckedEntryIds.Count > 0
                    ? selection.CheckedEntryIds
                    : await LoadCheckedEntryIdsAsync(
                        selection.CheckedEntriesAbsolutePath,
                        selection.CheckedEntriesRelativePath,
                        cancellationToken);

                var blueprint = ProjectBlueprintFactory.Create(selection, entry, checkedEntryIds, _userContext.UserName);
                _diagnostics.RecordStep($"Prepared project blueprint '{blueprint.ProjectId}' with name '{blueprint.Name}'.");

                var editor = _projectEditorFactory();
                editor.ConfigureRunReloadHandler(ct => ReloadBlueprintAsync(editor, ct));
                editor.Initialize(blueprint);

                var editorResult = _dialogService.ShowProjectEditor(editor);
                if (editorResult != true || editor.Result is null)
                {
                    _diagnostics.RecordStep("Project creation cancelled from project editor dialog.");
                    return null;
                }

                var committedBlueprint = editor.Result;
                var project = ProjectBlueprintConverter.ToReviewProject(committedBlueprint);

                _diagnostics.RecordStep($"Persisting review project '{project.Id}'.");
                await _workflowStore.SaveProjectAsync(project, cancellationToken).ConfigureAwait(false);
                TryRecordProjectFileState(project);

                var context = _hookContextFactory.CreateProjectCreated(project);
                await _reviewHookOrchestrator.ProcessAsync(project.Id, context, cancellationToken).ConfigureAwait(false);
                _diagnostics.RecordStep($"Executed review hook orchestrator for project '{project.Id}'.");

                var tags = BuildCreationTags(project, selection, entry, committedBlueprint.CheckedEntryIds);

                await ReviewChangeLogWriter.WriteAsync(
                    _changeLogOrchestrator,
                    project.Id,
                    _userContext.UserName,
                    "review.ui.project.created",
                    tags,
                    cancellationToken);

                _diagnostics.RecordStep($"Wrote change log entry for project '{project.Id}' as user '{_userContext.UserName}'.");

                return project;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _diagnostics.RecordException("Review project creation failed", ex);
                _messageBoxService.Show(
                    ex.Message,
                    "Create review project",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return null;
            }
        }

        public async Task<ReviewProject?> LoadProjectAsync(CancellationToken cancellationToken)
        {
            try
            {
                _diagnostics.RecordStep("Starting review project load flow.");
                var projectPath = PromptForProjectPath();
                if (projectPath is null)
                {
                    _diagnostics.RecordStep("Review project load cancelled before selecting a file.");
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();

                _diagnostics.RecordStep($"Attempting to load review project from '{projectPath}'.");

                var reference = ResolveProjectReference(projectPath);
                if (string.IsNullOrWhiteSpace(reference.ProjectId))
                {
                    _diagnostics.RecordStep("Selected file was not a review project JSON inside the workspace.");
                    _messageBoxService.Show(
                        "Select a review project JSON inside the workspace to continue.",
                        "Load review project",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return null;
                }

                var project = await _workflowStore.GetProjectAsync(reference.ProjectId!, cancellationToken);
                if (project is null)
                {
                    _diagnostics.RecordStep($"Cache miss for project '{reference.ProjectId}'. Refreshing store.");
                    await _workflowStore.GetProjectsAsync(cancellationToken);
                    project = await _workflowStore.GetProjectAsync(reference.ProjectId!, cancellationToken);
                }

                if (project is null)
                {
                    _diagnostics.RecordStep($"Project '{reference.ProjectId}' could not be loaded from workspace cache.");
                    _messageBoxService.Show(
                        $"Project '{reference.ProjectId}' could not be loaded from the workspace.",
                        "Load review project",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return null;
                }

                var tags = BuildLoadTags(project, reference);
                await ReviewChangeLogWriter.WriteAsync(
                    _changeLogOrchestrator,
                    project.Id,
                    _userContext.UserName,
                    "review.ui.project.load-requested",
                    tags,
                    cancellationToken);

                _diagnostics.RecordStep($"Recorded load request change log for project '{project.Id}'.");

                return project;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _diagnostics.RecordException("Review project load failed", ex);
                _messageBoxService.Show(
                    ex.Message,
                    "Load review project",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return null;
            }
        }

        private async Task<ProjectBlueprint?> ReloadBlueprintAsync(ProjectEditorViewModel editor, CancellationToken cancellationToken)
        {
            var reselection = await _runPicker.PickAsync(cancellationToken).ConfigureAwait(false);
            if (reselection is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _diagnostics.RecordStep($"Reselected LitSearch run '{reselection.RunId}' from entry '{reselection.EntryId}'.");

            var entry = await _entryStore.GetByIdAsync(reselection.EntryId, cancellationToken).ConfigureAwait(false);
            var checkedEntryIds = reselection.CheckedEntryIds.Count > 0
                ? reselection.CheckedEntryIds
                : await LoadCheckedEntryIdsAsync(
                    reselection.CheckedEntriesAbsolutePath,
                    reselection.CheckedEntriesRelativePath,
                    cancellationToken).ConfigureAwait(false);

            var blueprint = ProjectBlueprintFactory.Create(reselection, entry, checkedEntryIds, _userContext.UserName);

            var preservedStages = TryCaptureStageBlueprints(editor);
            var preservedName = string.IsNullOrWhiteSpace(editor.ProjectName) ? null : editor.ProjectName;
            var preservedNotes = editor.MetadataNotes;
            var preservedTemplate = editor.SelectedTemplateKind;

            return blueprint.With(
                name: preservedName,
                stages: preservedStages,
                template: preservedTemplate,
                metadataNotes: preservedNotes);
        }

        private static IReadOnlyList<StageBlueprint>? TryCaptureStageBlueprints(ProjectEditorViewModel editor)
        {
            if (editor.Stages.Count == 0)
            {
                return null;
            }

            var blueprints = new List<StageBlueprint>(editor.Stages.Count);
            foreach (var stage in editor.Stages)
            {
                if (!stage.TryBuild(out var blueprint, out _))
                {
                    return null;
                }

                blueprints.Add(blueprint);
            }

            return blueprints;
        }

        private string? PromptForProjectPath()
        {
            var selection = _dialogService.ShowOpenFileDialog(new FilePickerOptions
            {
                AllowMultiple = false,
                Filter = "Review project (project.json)|project.json|JSON files (*.json)|*.json|All files (*.*)|*.*"
            });

            if (selection is null || selection.Length == 0)
            {
                return null;
            }

            return selection[0];
        }


        private ProjectReference ResolveProjectReference(string absolutePath)
        {
            var workspaceRoot = _workspace.GetWorkspaceRoot();
            if (!absolutePath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Selected project file is outside of the active workspace.");
            }

            var fileName = Path.GetFileName(absolutePath);
            if (!string.Equals(fileName, "project.json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Select a project.json file to load a review project.");
            }

            var relativePath = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, absolutePath));
            string? projectId = null;
            var segments = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && string.Equals(segments[0], "reviews", StringComparison.OrdinalIgnoreCase))
            {
                projectId = segments[1];
            }

            return new ProjectReference(absolutePath, projectId, relativePath);
        }

        private static IEnumerable<string> BuildCreationTags(
            ReviewProject project,
            LitSearchRunSelection selection,
            Entry? entry,
            IReadOnlyCollection<string> checkedEntryIds)

        {
            var tags = new List<string>
            {
                $"projectId:{project.Id}",
                $"projectName:{project.Name}".Trim(),
                $"litsearchRun:{selection.RunId}",
                $"litsearchEntry:{selection.EntryId}",
                $"reviewTemplate:{project.Metadata.Template}"
            };

            if (checkedEntryIds.Count > 0)
            {
                tags.Add($"litsearchEntryCount:{checkedEntryIds.Count}");

                var preview = new List<string>(capacity: Math.Min(checkedEntryIds.Count, 5));
                var added = 0;
                foreach (var id in checkedEntryIds)
                {
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    preview.Add(id);
                    added++;
                    if (added == 5)
                    {
                        break;
                    }
                }

                if (preview.Count > 0)
                {
                    tags.Add($"litsearchEntryPreview:{string.Join(',', preview)}");
                }
            }
            else
            {
                tags.Add("litsearchEntryCount:0");
            }

            if (!string.IsNullOrWhiteSpace(selection.HookRelativePath))
            {
                tags.Add($"litsearchHook:{selection.HookRelativePath.Trim()}");

            }

            if (entry is not null)
            {
                var label = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Title : entry.DisplayName;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    tags.Add($"litsearchLabel:{label.Trim()}");
                }
            }

            return tags;
        }

        private async Task<IReadOnlyList<string>> LoadCheckedEntryIdsAsync(
            string? absolutePath,
            string? relativePath,
            CancellationToken cancellationToken)
        {
            var resolvedPath = ResolveCheckedEntriesPath(absolutePath, relativePath);
            if (resolvedPath is null)
            {
                return Array.Empty<string>();
            }

            try
            {
                await using var stream = new FileStream(
                    resolvedPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096,
                    useAsync: true);

                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("checkedEntries", out var checkedEntries) ||
                    !checkedEntries.TryGetProperty("entryIds", out var entryIdsElement) ||
                    entryIdsElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<string>();
                }

                var ids = new List<string>();
                foreach (var element in entryIdsElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var value = element.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            ids.Add(value.Trim());
                        }
                    }
                }

                return ids;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private string? ResolveCheckedEntriesPath(string? absolutePath, string? relativePath)
        {
            if (!string.IsNullOrWhiteSpace(absolutePath) && File.Exists(absolutePath))
            {
                return absolutePath;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            try
            {
                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var candidate = Path.IsPathRooted(normalized)
                    ? normalized
                    : Path.Combine(_workspace.GetWorkspaceRoot(), normalized);

                return File.Exists(candidate) ? candidate : null;
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
            {
                return null;
            }
        }

        private static IEnumerable<string> BuildLoadTags(ReviewProject project, ProjectReference reference)
        {
            var tags = new List<string>
            {
                $"projectId:{project.Id}",
                "trigger:manual-load"
            };

            if (!string.IsNullOrWhiteSpace(reference.RelativePath))
            {
                tags.Add($"projectPath:{reference.RelativePath!.Trim()}");
            }

            return tags;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }


        private sealed record ProjectReference(string AbsolutePath, string? ProjectId, string? RelativePath);

        private void TryRecordProjectFileState(ReviewProject project)
        {
            try
            {
                var workspaceRoot = _workspace.GetWorkspaceRoot();
                var projectDirectory = Path.Combine(workspaceRoot, "reviews", project.Id);
                var projectFile = Path.Combine(projectDirectory, "project.json");

                if (File.Exists(projectFile))
                {
                    _diagnostics.RecordStep($"Confirmed project file exists at '{projectFile}'.");
                }
                else
                {
                    _diagnostics.RecordStep($"Project file missing after save. Expected path '{projectFile}'.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PathTooLongException)
            {
                _diagnostics.RecordException("Failed to verify project file existence", ex);
            }
        }
    }
}
