using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;
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
        private readonly IDialogService _dialogs;
        private readonly IDataExtractionPowerPointExporter _powerPointExporter;
        private readonly IDataExtractionWordExporter _wordExporter;
        private readonly IDataExtractionExcelExporter _excelExporter;
        private CancellationTokenSource? _abstractLoadCts;
        private CancellationTokenSource? _exportAvailabilityCts;

        private bool _canExportPowerPoint;
        private bool _canExportWord;
        private bool _canExportExcel;

        private readonly AsyncRelayCommand<LibrarySearchResult?> _exportPowerPointCommand;
        private readonly AsyncRelayCommand<LibrarySearchResult?> _exportWordCommand;
        private readonly AsyncRelayCommand<LibrarySearchResult?> _exportExcelCommand;

        [ObservableProperty]
        private LibrarySearchResult? selected;

        [ObservableProperty]
        private bool hasLinkItems;

        [ObservableProperty]
        private string? selectedAbstract;

        private readonly List<LibrarySearchResult> _selectedItems = new();

        public event EventHandler? SelectionChanged;

        public ObservableCollection<LibraryLinkItem> LinkItems { get; }

        public IReadOnlyList<LibrarySearchResult> SelectedItems => _selectedItems;

        public bool HasSelection => _selectedItems.Count > 0;

        public bool HasSelectedAbstract => !string.IsNullOrWhiteSpace(SelectedAbstract);

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
                                       HookOrchestrator hookOrchestrator,
                                       IDialogService dialogs,
                                       IDataExtractionPowerPointExporter powerPointExporter,
                                       IDataExtractionWordExporter wordExporter,
                                       IDataExtractionExcelExporter excelExporter)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _entryEditor = entryEditor ?? throw new ArgumentNullException(nameof(entryEditor));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _attachmentPrompt = attachmentPrompt ?? throw new ArgumentNullException(nameof(attachmentPrompt));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _powerPointExporter = powerPointExporter ?? throw new ArgumentNullException(nameof(powerPointExporter));
            _wordExporter = wordExporter ?? throw new ArgumentNullException(nameof(wordExporter));
            _excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));

            Items = new ObservableCollection<LibrarySearchResult>();
            LinkItems = new ObservableCollection<LibraryLinkItem>();

            _exportPowerPointCommand = new AsyncRelayCommand<LibrarySearchResult?>(ExportPowerPointAsync, _ => _canExportPowerPoint);
            _exportWordCommand = new AsyncRelayCommand<LibrarySearchResult?>(ExportWordAsync, _ => _canExportWord);
            _exportExcelCommand = new AsyncRelayCommand<LibrarySearchResult?>(ExportExcelAsync, _ => _canExportExcel);
        }

        public ObservableCollection<LibrarySearchResult> Items { get; }

        public IAsyncRelayCommand ExportExtractionToPowerPointCommand => _exportPowerPointCommand;

        public IAsyncRelayCommand ExportExtractionToWordCommand => _exportWordCommand;

        public IAsyncRelayCommand ExportExtractionToExcelCommand => _exportExcelCommand;

        [RelayCommand]
        private void HandleSelectionChanged(IList? selectedItems)
        {
            _selectedItems.Clear();

            if (selectedItems is not null)
            {
                foreach (var item in selectedItems)
                {
                    if (item is LibrarySearchResult result && !_selectedItems.Contains(result))
                        _selectedItems.Add(result);
                }
            }

            if (_selectedItems.Count == 0 && Selected is not null)
                _selectedItems.Add(Selected);

            OnPropertyChanged(nameof(SelectedItems));
            OnPropertyChanged(nameof(HasSelection));
            RaiseSelectionChanged();

            _ = UpdateExporterAvailabilityAsync();
        }

        public void Clear()
        {
            ExecuteOnDispatcher(() =>
            {
                Items.Clear();
                Selected = null;
                ResultsAreFullText = false;
            });

            SetExportAvailability(false, false, false);
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

        [RelayCommand]
        private void OpenAttachment(Attachment? attachment)
        {
            if (attachment is null)
                return;

            if (string.IsNullOrWhiteSpace(attachment.RelativePath))
            {
                System.Windows.MessageBox.Show(
                    "Attachment does not have an associated file path.",
                    "Open Attachment",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var absolutePath = _workspace.GetAbsolutePath(attachment.RelativePath);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                System.Windows.MessageBox.Show(
                    $"Attachment not found:\n{attachment.RelativePath}",
                    "Open Attachment",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            try
            {
                _documentService.OpenAttachment(attachment);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open attachment:\n{ex.Message}",
                    "Open Attachment",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenLink(LibraryLinkItem? link)
        {
            if (link is null)
                return;

            try
            {
                if (link.Kind == LinkItemKind.Folder)
                {
                    if (!Directory.Exists(link.Target))
                    {
                        System.Windows.MessageBox.Show(
                            $"Folder not found:\n{link.Target}",
                            "Open Link",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }
                }
                else if (link.Kind == LinkItemKind.File)
                {
                    if (!File.Exists(link.Target))
                    {
                        System.Windows.MessageBox.Show(
                            $"File not found:\n{link.Target}",
                            "Open Link",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }
                }

                var info = new ProcessStartInfo
                {
                    FileName = link.Target,
                    UseShellExecute = true
                };

                Process.Start(info);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open link:\n{ex.Message}",
                    "Open Link",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
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
        private Task EditAsync() => EditEntryAsync(Selected);

        private bool CanModifySelected() => Selected?.Entry is not null;

        partial void OnSelectedChanged(LibrarySearchResult? value)
        {
            OpenCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            UpdateLinkItems();
            SyncSelectionList(value);
            RaiseSelectionChanged();
            _ = LoadSelectedAbstractAsync(value);
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

        private async Task ExportPowerPointAsync(LibrarySearchResult? result)
            => await ExportAsync(result,
                "PowerPoint (*.pptx)|*.pptx|All files (*.*)|*.*",
                "-extraction.pptx",
                _powerPointExporter.ExportAsync,
                "PowerPoint").ConfigureAwait(false);

        private async Task ExportWordAsync(LibrarySearchResult? result)
            => await ExportAsync(result,
                "Word (*.docx)|*.docx|All files (*.*)|*.*",
                "-extraction.docx",
                _wordExporter.ExportAsync,
                "Word").ConfigureAwait(false);

        private async Task ExportExcelAsync(LibrarySearchResult? result)
            => await ExportAsync(result,
                "Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                "-extraction.xlsx",
                _excelExporter.ExportAsync,
                "Excel").ConfigureAwait(false);

        private async Task ExportAsync(LibrarySearchResult? result,
                                       string filter,
                                       string suffix,
                                       Func<string, string, CancellationToken, Task<string>> exporter,
                                       string artifactLabel)
        {
            var entry = (result ?? Selected)?.Entry;
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                return;
            }

            var defaultName = BuildDefaultFileName(entry, suffix);
            var targetPath = _dialogs.ShowSaveFileDialog(new FileSavePickerOptions
            {
                Filter = filter,
                DefaultFileName = defaultName
            });

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            try
            {
                await exporter(entry.Id, targetPath, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryResultsViewModel] {artifactLabel} export failed: {ex}");
                System.Windows.MessageBox.Show(
                    $"Failed to export {artifactLabel.ToLowerInvariant()} assets: {ex.Message}",
                    "Export failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task UpdateExporterAvailabilityAsync()
        {
            var entry = Selected?.Entry;
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                SetExportAvailability(false, false, false);
                return;
            }

            var cts = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref _exportAvailabilityCts, cts);
            previous?.Cancel();
            previous?.Dispose();

            try
            {
                var tasks = new[]
                {
                    _powerPointExporter.CanExportAsync(entry.Id, cts.Token),
                    _wordExporter.CanExportAsync(entry.Id, cts.Token),
                    _excelExporter.CanExportAsync(entry.Id, cts.Token)
                };

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                SetExportAvailability(results[0], results[1], results[2]);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryResultsViewModel] Failed to evaluate export availability: {ex}");
                SetExportAvailability(false, false, false);
            }
            finally
            {
                if (ReferenceEquals(_exportAvailabilityCts, cts))
                {
                    _exportAvailabilityCts = null;
                }

                cts.Dispose();
            }
        }

        private void SetExportAvailability(bool powerPoint, bool word, bool excel)
        {
            ExecuteOnDispatcher(() =>
            {
                _canExportPowerPoint = powerPoint;
                _canExportWord = word;
                _canExportExcel = excel;
                _exportPowerPointCommand.NotifyCanExecuteChanged();
                _exportWordCommand.NotifyCanExecuteChanged();
                _exportExcelCommand.NotifyCanExecuteChanged();
            });
        }

        private static string BuildDefaultFileName(Entry entry, string suffix)
        {
            var baseName = entry.DisplayName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = entry.Title;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = entry.Id;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "entry";

            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(baseName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return sanitized + suffix;
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

        private LibrarySearchResult? EnsureSelection(LibrarySearchResult? candidate)
        {
            if (candidate is not null && !ReferenceEquals(Selected, candidate))
                Selected = candidate;

            return candidate ?? Selected;
        }

        private void SyncSelectionList(LibrarySearchResult? value)
        {
            if (value is null)
            {
                if (_selectedItems.Count > 0)
                {
                    _selectedItems.Clear();
                    OnPropertyChanged(nameof(SelectedItems));
                    OnPropertyChanged(nameof(HasSelection));
                }
                return;
            }

            if (_selectedItems.Count == 0 || !_selectedItems.Contains(value))
            {
                _selectedItems.Clear();
                _selectedItems.Add(value);
                OnPropertyChanged(nameof(SelectedItems));
            }

            OnPropertyChanged(nameof(HasSelection));
        }

        private void RaiseSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateLinkItems()
        {
            LinkItems.Clear();

            var entry = Selected?.Entry;
            if (entry is null)
            {
                HasLinkItems = false;
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddLink(string display, string target, LinkItemKind kind)
            {
                if (string.IsNullOrWhiteSpace(target))
                    return;

                var normalizedTarget = target.Trim();
                if (normalizedTarget.Length == 0)
                    return;

                if (!seen.Add(normalizedTarget))
                    return;

                var normalizedDisplay = string.IsNullOrWhiteSpace(display)
                    ? normalizedTarget
                    : display.Trim();

                LinkItems.Add(new LibraryLinkItem(normalizedDisplay, normalizedTarget, kind));
            }

            if (entry.Links is { Count: > 0 })
            {
                foreach (var raw in entry.Links)
                {
                    var trimmed = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    var kind = DetermineLinkKind(trimmed);
                    var target = trimmed;

                    if (kind != LinkItemKind.Url && !Path.IsPathRooted(trimmed))
                    {
                        var absolute = _workspace.GetAbsolutePath(trimmed);
                        if (!string.IsNullOrWhiteSpace(absolute))
                            target = absolute;
                    }

                    AddLink(trimmed, target, kind);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Pmid))
            {
                var pmid = entry.Pmid.Trim();
                if (pmid.Length > 0)
                {
                    var url = $"https://pubmed.ncbi.nlm.nih.gov/{pmid.TrimEnd('/')}/";
                    AddLink(url, url, LinkItemKind.Url);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Doi))
            {
                var doi = entry.Doi.Trim();
                if (doi.Length > 0)
                {
                    var url = doi.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                              doi.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? doi
                        : $"https://doi.org/{doi}";
                    AddLink(url, url, LinkItemKind.Url);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                var folderRelative = Path.Combine("entries", entry.Id);
                var folderAbsolute = _workspace.GetAbsolutePath(folderRelative);
                if (!string.IsNullOrWhiteSpace(folderAbsolute))
                    AddLink(folderAbsolute, folderAbsolute, LinkItemKind.Folder);
            }

            HasLinkItems = LinkItems.Count > 0;
        }

        private static LinkItemKind DetermineLinkKind(string candidate)
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return LinkItemKind.Url;
                }

                if (uri.IsFile)
                    return LinkItemKind.File;
            }

            return candidate.Contains("://", StringComparison.Ordinal)
                ? LinkItemKind.Url
                : LinkItemKind.File;
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

            entry.Source = string.IsNullOrWhiteSpace(entry.Source) ? null : entry.Source.Trim();

            if (entry.Links is null || entry.Links.Count == 0)
            {
                entry.Links = entry.Links ?? new List<string>();
            }
            else
            {
                entry.Links = entry.Links
                    .Where(link => !string.IsNullOrWhiteSpace(link))
                    .Select(link => link.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        partial void OnSelectedAbstractChanged(string? value)
        {
            OnPropertyChanged(nameof(HasSelectedAbstract));
        }

        private async Task LoadSelectedAbstractAsync(LibrarySearchResult? result)
        {
            _abstractLoadCts?.Cancel();
            _abstractLoadCts?.Dispose();

            if (result is null || string.IsNullOrWhiteSpace(result.Entry?.Id))
            {
                ExecuteOnDispatcher(() => SelectedAbstract = null);
                return;
            }

            ExecuteOnDispatcher(() => SelectedAbstract = null);

            var cts = new CancellationTokenSource();
            _abstractLoadCts = cts;
            var token = cts.Token;

            try
            {
                var entryId = result.Entry!.Id!;
                var article = await LoadArticleHookAsync(entryId).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var text = ComposeAbstractText(article?.Abstract);
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = result.Entry.Notes;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                var normalized = NormalizeAbstract(text);
                ExecuteOnDispatcher(() => SelectedAbstract = normalized);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryResultsViewModel] Failed to load abstract: {ex}");
                if (!token.IsCancellationRequested)
                {
                    var fallback = NormalizeAbstract(result.Entry?.Notes);
                    ExecuteOnDispatcher(() => SelectedAbstract = fallback);
                }
            }
        }

        private static string? NormalizeAbstract(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static string? ComposeAbstractText(HookM.ArticleAbstract? abstractHook)
        {
            if (abstractHook is null)
            {
                return null;
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(abstractHook.Text))
            {
                parts.Add(abstractHook.Text.Trim());
            }

            if (abstractHook.Sections is { Count: > 0 })
            {
                foreach (var section in abstractHook.Sections)
                {
                    if (section is null)
                    {
                        continue;
                    }

                    var content = string.IsNullOrWhiteSpace(section.Text) ? null : section.Text.Trim();
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }

                    var label = string.IsNullOrWhiteSpace(section.Label) ? null : section.Label.Trim();
                    parts.Add(label is null ? content! : string.Concat(label, ": ", content));
                }
            }

            if (parts.Count == 0)
            {
                return null;
            }

            return string.Join(Environment.NewLine + Environment.NewLine, parts);
        }

        public async Task EditEntryAsync(LibrarySearchResult? target)
        {
            var resolved = EnsureSelection(target);
            var entry = resolved?.Entry;
            if (entry is null)
                return;

            try
            {
                var saved = await _entryEditor.EditEntryAsync(entry).ConfigureAwait(false);
                if (saved && !string.IsNullOrWhiteSpace(entry.Id))
                    await RefreshSelectedEntryAsync(resolved, entry.Id!).ConfigureAwait(false);
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
    }
}
