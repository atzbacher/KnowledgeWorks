using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Search;

namespace LM.App.Wpf.ViewModels.Library
{
    /// <summary>
    /// Handles Library search result presentation and document operations.
    /// </summary>
    public sealed class LibraryResultsViewModel : ViewModelBase
    {
        private static readonly HashSet<string> s_supportedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt", ".md"
        };

        private readonly IEntryStore _store;
        private readonly IFileStorageRepository _storage;
        private readonly ILibraryEntryEditor _entryEditor;
        private readonly ILibraryDocumentService _documentService;
        private readonly RelayCommand _openCommand;
        private readonly RelayCommand _editCommand;
        private LibrarySearchResult? _selected;
        private bool _resultsAreFullText;

        public LibraryResultsViewModel(IEntryStore store,
                                       IFileStorageRepository storage,
                                       ILibraryEntryEditor entryEditor,
                                       ILibraryDocumentService documentService)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _entryEditor = entryEditor ?? throw new ArgumentNullException(nameof(entryEditor));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));

            Items = new ObservableCollection<LibrarySearchResult>();
            _openCommand = new RelayCommand(_ => OpenSelected(), _ => Selected?.Entry is not null);
            _editCommand = new RelayCommand(_ => EditSelected(), _ => Selected?.Entry is not null);
        }

        public ObservableCollection<LibrarySearchResult> Items { get; }

        public LibrarySearchResult? Selected
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value))
                    return;
                _selected = value;
                OnPropertyChanged();
                _openCommand.RaiseCanExecuteChanged();
                _editCommand.RaiseCanExecuteChanged();
            }
        }

        public bool ResultsAreFullText
        {
            get => _resultsAreFullText;
            private set
            {
                if (_resultsAreFullText == value)
                    return;
                _resultsAreFullText = value;
                OnPropertyChanged();
            }
        }

        public ICommand OpenCommand => _openCommand;
        public ICommand EditCommand => _editCommand;

        public void Clear()
        {
            Items.Clear();
            Selected = null;
            ResultsAreFullText = false;
        }

        public void LoadMetadataResults(IEnumerable<Entry> entries)
        {
            Clear();
            foreach (var entry in entries)
            {
                PrepareEntry(entry);
                Items.Add(new LibrarySearchResult(entry, null, null));
            }
        }

        public async Task LoadFullTextResultsAsync(IReadOnlyList<FullTextSearchHit> hits)
        {
            Clear();
            ResultsAreFullText = true;
            foreach (var hit in hits)
            {
                var entry = await _store.GetByIdAsync(hit.EntryId).ConfigureAwait(false);
                if (entry is null)
                    continue;

                PrepareEntry(entry);
                Items.Add(new LibrarySearchResult(entry, hit.Score, hit.Highlight));
            }
        }

        public void MarkAsMetadataResults() => ResultsAreFullText = false;

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

            var attachments = entry.Attachments ??= new List<Attachment>();
            var existing = new HashSet<string>(attachments.Select(a => a.RelativePath), StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<string>();
            var failures = new List<string>();
            var added = new List<Attachment>();
            var targetDir = Path.Combine("attachments", entryId);

            foreach (var path in candidates)
            {
                try
                {
                    var relative = await _storage.SaveNewAsync(path, targetDir).ConfigureAwait(false);
                    if (!existing.Add(relative))
                    {
                        duplicates.Add(DisplayNameForPath(path));
                        continue;
                    }

                    var attachment = new Attachment { RelativePath = relative };
                    attachments.Add(attachment);
                    added.Add(attachment);
                }
                catch (Exception ex)
                {
                    failures.Add($"{DisplayNameForPath(path)} — {ex.Message}");
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

            await RefreshSelectedEntryAsync(targetResult, entryId).ConfigureAwait(false);

            ShowDropWarnings(unsupported, duplicates, failures);
        }

        private void OpenSelected()
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

        private void EditSelected()
        {
            var entry = Selected?.Entry;
            if (entry is null) return;

            try
            {
                _entryEditor.EditEntry(entry);
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

        private async Task RefreshSelectedEntryAsync(LibrarySearchResult? previous, string entryId)
        {
            try
            {
                var updated = await _store.GetByIdAsync(entryId).ConfigureAwait(false);
                if (updated is null)
                    return;

                PrepareEntry(updated);

                LibrarySearchResult newResult;
                if (previous is not null)
                {
                    newResult = new LibrarySearchResult(updated, previous.Score, previous.Highlight);
                    var index = Items.IndexOf(previous);
                    if (index >= 0)
                        Items[index] = newResult;
                }
                else
                {
                    newResult = new LibrarySearchResult(updated, null, null);
                }

                Selected = newResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryResultsViewModel] RefreshSelectedEntryAsync failed: {ex}");
            }
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
