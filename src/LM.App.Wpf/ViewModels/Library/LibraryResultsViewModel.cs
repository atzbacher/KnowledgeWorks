using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.Views.Behaviors;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Search;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library
{
    /// <summary>
    /// Handles Library search result presentation and document operations.
    /// </summary>
    public sealed partial class LibraryResultsViewModel : ViewModelBase
    {
        private static readonly HashSet<string> s_supportedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt", ".md"
        };

        private readonly IEntryStore _store;
        private readonly IFileStorageRepository _storage;
        private readonly ILibraryEntryEditor _entryEditor;
        private readonly ILibraryDocumentService _documentService;
        private readonly IAttachmentMetadataPrompt _attachmentPrompt;
        private readonly IWorkSpaceService _workspace;
        private readonly HookOrchestrator _hookOrchestrator;

        [ObservableProperty]
        private LibrarySearchResult? selected;

        private bool _resultsAreFullText;

        public bool ResultsAreFullText
        {
            get => _resultsAreFullText;
            private set => SetProperty(ref _resultsAreFullText, value);
        }

        public LibraryResultsViewModel(IEntryStore store,
                                       IFileStorageRepository storage,
                                       ILibraryEntryEditor entryEditor,
                                       ILibraryDocumentService documentService,
                                       IAttachmentMetadataPrompt attachmentPrompt,
                                       IWorkSpaceService workspace,
                                       HookOrchestrator hookOrchestrator)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _entryEditor = entryEditor ?? throw new ArgumentNullException(nameof(entryEditor));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _attachmentPrompt = attachmentPrompt ?? throw new ArgumentNullException(nameof(attachmentPrompt));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));

            Items = new ObservableCollection<LibrarySearchResult>();
        }

        public ObservableCollection<LibrarySearchResult> Items { get; }

        public void Clear()
        {
            ExecuteOnDispatcher(() =>
            {
                Items.Clear();
                Selected = null;
                ResultsAreFullText = false;
            });
        }

        public void LoadMetadataResults(IEnumerable<Entry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            Clear();

            var results = new List<LibrarySearchResult>();

            foreach (var entry in entries)
            {
                if (entry is null)
                    continue;

                PrepareEntry(entry);
                results.Add(new LibrarySearchResult(entry, null, null));
            }

            if (results.Count == 0)
                return;

            ExecuteOnDispatcher(() =>
            {
                foreach (var result in results)
                {
                    Items.Add(result);
                }
            });
        }

        public async Task LoadFullTextResultsAsync(IReadOnlyList<FullTextSearchHit> hits)
        {
            ArgumentNullException.ThrowIfNull(hits);

            ExecuteOnDispatcher(() =>
            {
                Items.Clear();
                Selected = null;
                ResultsAreFullText = true;
            });

            foreach (var hit in hits)
            {
                var entry = await _store.GetByIdAsync(hit.EntryId).ConfigureAwait(false);
                if (entry is null)
                    continue;

                PrepareEntry(entry);
                var result = new LibrarySearchResult(entry, hit.Score, hit.Highlight);

                ExecuteOnDispatcher(() => Items.Add(result));
            }
        }

        public void MarkAsMetadataResults()
        {
            ExecuteOnDispatcher(() => ResultsAreFullText = false);
        }

        public bool CanAcceptFileDrop(IEnumerable<string>? filePaths, LibrarySearchResult? dropTarget = null)
        {
            if (filePaths is null)
                return false;

            var entry = (dropTarget ?? Selected)?.Entry;
            if (entry is null)
                return false;

            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                if (!File.Exists(path))
                    continue;
                if (IsSupportedAttachment(path))
                    return true;
            }

            return false;
        }

        [RelayCommand]
        private void PreviewDrop(FileDropRequest request)
        {
            if (request is null)
                return;

            var dropTarget = request.DropTarget as LibrarySearchResult;
            var canAccept = CanAcceptFileDrop(request.Paths, dropTarget);

            request.Args.Effects = canAccept
                ? System.Windows.DragDropEffects.Copy
                : System.Windows.DragDropEffects.None;
            request.Args.Handled = true;
        }

        [RelayCommand]
        private async Task DropAsync(FileDropRequest request)
        {
            if (request is null)
                return;

            if (request.Paths.Count == 0)
            {
                request.Args.Effects = System.Windows.DragDropEffects.None;
                request.Args.Handled = true;
                return;
            }

            var dropTarget = request.DropTarget as LibrarySearchResult;

            if (dropTarget is not null && !ReferenceEquals(Selected, dropTarget))
                Selected = dropTarget;

            await HandleFileDropAsync(request.Paths, dropTarget).ConfigureAwait(false);

            request.Args.Handled = true;
        }

        public async Task HandleFileDropAsync(IEnumerable<string>? filePaths, LibrarySearchResult? dropTarget = null)
        {
            var targetResult = dropTarget ?? Selected;
            var entry = targetResult?.Entry;

            if (entry is null)
            {
                System.Windows.MessageBox.Show(
                    "Select an entry before adding attachments.",
                    "Add Attachments",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            if (filePaths is null)
                return;

            var normalized = filePaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(File.Exists)
                .ToList();

            if (normalized.Count == 0)
                return;

            var unsupported = new List<string>();
            var candidates = new List<string>();

            foreach (var path in normalized)
            {
                if (IsSupportedAttachment(path))
                    candidates.Add(path);
                else
                    unsupported.Add(DisplayNameForPath(path));
            }

            if (candidates.Count == 0)
            {
                if (unsupported.Count > 0)
                {
                    System.Windows.MessageBox.Show(
                        "Unsupported file types were skipped:\n" + string.Join(Environment.NewLine, unsupported),
                        "Add Attachments",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }

                return;
            }

            var entryId = entry.Id;
            if (string.IsNullOrWhiteSpace(entryId))
            {
                System.Windows.MessageBox.Show(
                    "The selected entry is missing an identifier and cannot receive attachments.",
                    "Add Attachments",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            var metadata = await RequestAttachmentMetadataAsync(entry, candidates).ConfigureAwait(false);
            if (metadata is null || metadata.Attachments.Count == 0)
                return;

            var metadataByPath = new Dictionary<string, AttachmentMetadataSelection>(StringComparer.OrdinalIgnoreCase);
            foreach (var selection in metadata.Attachments)
            {
                if (selection is null)
                    continue;
                if (string.IsNullOrWhiteSpace(selection.SourcePath))
                    continue;

                metadataByPath[selection.SourcePath] = selection;
            }

            if (metadataByPath.Count == 0)
                return;

            var attachments = entry.Attachments ??= new List<Attachment>();
            var existing = new HashSet<string>(attachments.Select(a => a.RelativePath), StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<string>();
            var failures = new List<string>();
            var added = new List<Attachment>();
            var targetDir = Path.Combine("attachments", entryId);
            var addedBy = GetCurrentUserName();

            foreach (var path in candidates)
            {
                if (!metadataByPath.TryGetValue(path, out var selection))
                    continue;

                var display = string.IsNullOrWhiteSpace(selection.Title)
                    ? DisplayNameForPath(path)
                    : selection.Title.Trim();

                try
                {
                    var relative = await _storage.SaveNewAsync(path, targetDir).ConfigureAwait(false);
                    if (!existing.Add(relative))
                    {
                        duplicates.Add(display);
                        continue;
                    }

                    var tags = selection.Tags is { Count: > 0 }
                        ? new List<string>(selection.Tags)
                        : new List<string>();

                    var attachment = new Attachment
                    {
                        RelativePath = relative,
                        Title = display,
                        Kind = selection.Kind,
                        Tags = tags,
                        AddedBy = addedBy,
                        AddedUtc = DateTime.UtcNow
                    };

                    attachments.Add(attachment);
                    added.Add(attachment);
                }
                catch (Exception ex)
                {
                    failures.Add($"{display} — {ex.Message}");
                }
            }

            if (added.Count == 0)
            {
                ShowDropWarnings(unsupported, duplicates, failures);
                return;
            }

            try
            {
                await _store.SaveAsync(entry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                foreach (var attachment in added)
                    attachments.Remove(attachment);

                System.Windows.MessageBox.Show(
                    $"Failed to save entry with new attachments:\n{ex.Message}",
                    "Add Attachments",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            await PersistAttachmentHooksAsync(entry, added, entryId).ConfigureAwait(false);

            await RefreshSelectedEntryAsync(targetResult, entryId).ConfigureAwait(false);

            ShowDropWarnings(unsupported, duplicates, failures);
        }

        [RelayCommand(CanExecute = nameof(CanModifySelected))]
        private void Open()
        {
            var entry = Selected?.Entry;
            if (entry is null) return;

            try
            {
                _documentService.OpenEntry(entry);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open file:\n{ex.Message}",
                    "Open Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelected))]
        private async Task EditAsync()
        {
            var previous = Selected;
            var entry = previous?.Entry;
            if (entry is null)
                return;

            try
            {
                var saved = await _entryEditor.EditEntryAsync(entry).ConfigureAwait(false);
                if (saved && !string.IsNullOrWhiteSpace(entry.Id))
                    await RefreshSelectedEntryAsync(previous, entry.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open entry editor:\n{ex.Message}",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanModifySelected() => Selected?.Entry is not null;

        partial void OnSelectedChanged(LibrarySearchResult? value)
        {
            OpenCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
        }

        private async Task RefreshSelectedEntryAsync(LibrarySearchResult? previous, string entryId)
        {
            try
            {
                var updated = await _store.GetByIdAsync(entryId).ConfigureAwait(false);
                if (updated is null)
                    return;

                PrepareEntry(updated);

                var newResult = previous is not null
                    ? new LibrarySearchResult(updated, previous.Score, previous.Highlight)
                    : new LibrarySearchResult(updated, null, null);

                ExecuteOnDispatcher(() =>
                {
                    if (previous is not null)
                    {
                        var index = Items.IndexOf(previous);
                        if (index >= 0)
                            Items[index] = newResult;
                    }

                    Selected = newResult;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryResultsViewModel] RefreshSelectedEntryAsync failed: {ex}");
            }
        }

        private Task<AttachmentMetadataPromptResult?> RequestAttachmentMetadataAsync(Entry entry, IReadOnlyCollection<string> paths)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            if (paths is null)
                throw new ArgumentNullException(nameof(paths));

            var label = !string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DisplayName!
                : (!string.IsNullOrWhiteSpace(entry.Title) ? entry.Title! : entry.Id ?? string.Empty);

            var context = new AttachmentMetadataPromptContext(label, paths.ToList());
            return _attachmentPrompt.RequestMetadataAsync(context);
        }

        private async Task PersistAttachmentHooksAsync(Entry entry, IReadOnlyList<Attachment> added, string entryId)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(entryId))
                return;

            var article = await TryMergeArticleHookAsync(entry, added, entryId).ConfigureAwait(false);

            var ctx = new HookContext
            {
                Article = article,
                Attachments = BuildAttachmentHook(entry),
                ChangeLog = BuildChangeLogHook(added)
            };

            try
            {
                await _hookOrchestrator.ProcessAsync(entryId, ctx, System.Threading.CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryResultsViewModel] Failed to persist attachment hooks: {ex}");
            }
        }

        private static HookM.AttachmentHook BuildAttachmentHook(Entry entry)
        {
            var hook = new HookM.AttachmentHook();
            var items = new List<HookM.AttachmentHookItem>();

            if (entry.Attachments is not null)
            {
                foreach (var attachment in entry.Attachments)
                {
                    if (attachment is null)
                        continue;

                    var title = string.IsNullOrWhiteSpace(attachment.Title)
                        ? attachment.RelativePath
                        : attachment.Title.Trim();

                    var tags = attachment.Tags is { Count: > 0 }
                        ? new List<string>(attachment.Tags)
                        : new List<string>();

                    items.Add(new HookM.AttachmentHookItem
                    {
                        AttachmentId = attachment.Id,
                        Title = title,
                        LibraryPath = attachment.RelativePath,
                        Tags = tags,
                        Notes = attachment.Notes,
                        Purpose = attachment.Kind,
                        AddedBy = string.IsNullOrWhiteSpace(attachment.AddedBy) ? "unknown" : attachment.AddedBy,
                        AddedUtc = attachment.AddedUtc == default ? DateTime.UtcNow : attachment.AddedUtc
                    });
                }
            }

            hook.Attachments = items;
            return hook;
        }

        private static HookM.EntryChangeLogHook? BuildChangeLogHook(IReadOnlyList<Attachment> added)
        {
            if (added is null || added.Count == 0)
                return null;

            var events = new List<HookM.EntryChangeLogEvent>();

            foreach (var attachment in added)
            {
                if (attachment is null)
                    continue;

                var performedBy = string.IsNullOrWhiteSpace(attachment.AddedBy)
                    ? "unknown"
                    : attachment.AddedBy;

                var title = string.IsNullOrWhiteSpace(attachment.Title)
                    ? attachment.RelativePath
                    : attachment.Title;

                var tags = attachment.Tags is { Count: > 0 }
                    ? new List<string>(attachment.Tags)
                    : new List<string>();

                events.Add(new HookM.EntryChangeLogEvent
                {
                    EventId = Guid.NewGuid().ToString("N"),
                    TimestampUtc = attachment.AddedUtc == default ? DateTime.UtcNow : attachment.AddedUtc,
                    PerformedBy = performedBy,
                    Action = "AttachmentAdded",
                    Details = new HookM.ChangeLogAttachmentDetails
                    {
                        AttachmentId = attachment.Id,
                        Title = title,
                        LibraryPath = attachment.RelativePath,
                        Purpose = attachment.Kind,
                        Tags = tags
                    }
                });
            }

            if (events.Count == 0)
                return null;

            return new HookM.EntryChangeLogHook
            {
                Events = events
            };
        }

        private static string GetCurrentUserName()
        {
            var user = Environment.UserName;
            return string.IsNullOrWhiteSpace(user) ? "unknown" : user;
        }

        private static void ExecuteOnDispatcher(Action action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(action);
                return;
            }

            action();
        }

        private static bool IsSupportedAttachment(string path)
        {
            var ext = Path.GetExtension(path);
            return !string.IsNullOrWhiteSpace(ext) && s_supportedAttachmentExtensions.Contains(ext);
        }

        private static string DisplayNameForPath(string path)
        {
            var name = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? path : name!;
        }

        private static void ShowDropWarnings(IReadOnlyCollection<string> unsupported,
                                             IReadOnlyCollection<string> duplicates,
                                             IReadOnlyCollection<string> failures)
        {
            if (unsupported.Count == 0 && duplicates.Count == 0 && failures.Count == 0)
                return;

            var sections = new List<string>();

            if (unsupported.Count > 0)
                sections.Add("Unsupported files:\n" + string.Join(Environment.NewLine, unsupported));

            if (duplicates.Count > 0)
                sections.Add("Already attached:\n" + string.Join(Environment.NewLine, duplicates));

            if (failures.Count > 0)
                sections.Add("Failed to add:\n" + string.Join(Environment.NewLine, failures));

            var icon = failures.Count > 0
                ? System.Windows.MessageBoxImage.Warning
                : System.Windows.MessageBoxImage.Information;

            System.Windows.MessageBox.Show(
                string.Join(Environment.NewLine + Environment.NewLine, sections),
                "Add Attachments",
                System.Windows.MessageBoxButton.OK,
                icon);
        }

        private static void PrepareEntry(Entry entry)
        {
            var title = string.IsNullOrWhiteSpace(entry.Title) ? "(untitled)" : entry.Title!.Trim();
            entry.Title = title;

            var displayName = title;
            if (entry.Authors != null && entry.Authors.Count > 0)
                displayName += $" — {string.Join(", ", entry.Authors)}";

            entry.DisplayName = displayName;
        }
    }
}
