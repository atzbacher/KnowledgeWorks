using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Infrastructure.Utils;

namespace LM.App.Wpf.ViewModels
{
    public sealed partial class LibraryViewModel
    {
        private static readonly (string Key, string Display)[] s_columnDefinitions =
        {
            ("AttachmentIndicator", "Attachments"),
            ("Title", "Title"),
            ("Score", "Score"),
            ("Source", "Source"),
            ("Type", "Type"),
            ("Year", "Year"),
            ("AddedOn", "Added on"),
            ("AddedBy", "Added by"),
            ("InternalId", "Internal ID"),
            ("Doi", "DOI"),
            ("Pmid", "PMID"),
            ("Nct", "NCT"),
            ("Authors", "Authors"),
            ("Tags", "Tags"),
            ("IsInternal", "Internal"),
            ("Snippet", "Snippet")
        };

        private readonly IWorkSpaceService _workspace;
        private readonly IUserPreferencesStore _preferencesStore;
        private readonly IClipboardService _clipboard;
        private readonly IFileExplorerService _fileExplorer;
        private readonly ILibraryDocumentService _documentService;
        private readonly Func<Entry, CancellationToken, Task<bool>> _dataExtractionLauncher;
        private readonly LibraryColumnVisibility _columnVisibility = new();
        private readonly List<LibraryColumnOption> _columnOptions = new();
        private readonly TaskCompletionSource<bool> _preferencesReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private UserPreferences _preferences = new();
        private bool _suppressColumnPersistence;

        public LibraryColumnVisibility ColumnVisibility => _columnVisibility;

        public IReadOnlyList<LibraryColumnOption> ColumnOptions => _columnOptions;

        private void InitializeColumns()
        {
            foreach (var (key, display) in s_columnDefinitions)
            {
                var option = new LibraryColumnOption(key, display, true);
                option.PropertyChanged += OnColumnOptionChanged;
                _columnOptions.Add(option);
                _columnVisibility[key] = true;
            }
        }

        private void OnResultsSelectionChanged(object? sender, EventArgs e)
        {
            OpenEntryCommand.NotifyCanExecuteChanged();
            OpenContainingFolderCommand.NotifyCanExecuteChanged();
            CopyMetadataCommand.NotifyCanExecuteChanged();
            CopyWorkspacePathCommand.NotifyCanExecuteChanged();
            EditEntryCommand.NotifyCanExecuteChanged();
            OpenDataExtractionCommand.NotifyCanExecuteChanged();
        }

        private void OnColumnOptionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not LibraryColumnOption option)
                return;

            if (e.PropertyName is not nameof(LibraryColumnOption.IsVisible))
                return;

            _columnVisibility[option.Key] = option.IsVisible;
            if (_suppressColumnPersistence)
                return;

            _ = PersistColumnPreferencesAsync();
        }

        private async Task LoadPreferencesAsync()
        {
            try
            {
                var loaded = await _preferencesStore.LoadAsync(CancellationToken.None).ConfigureAwait(false);
                if (loaded is not null)
                {
                    _preferences = loaded;
                    ApplyColumnPreferences(loaded.Library);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LibraryViewModel] Failed to load preferences: {ex}");
            }
            finally
            {
                _preferencesReady.TrySetResult(true);
            }
        }

        private void ApplyColumnPreferences(LibraryPreferences? preferences)
        {
            if (preferences is null)
                return;

            var visible = preferences.VisibleColumns ?? Array.Empty<string>();
            var visibleSet = new HashSet<string>(visible, StringComparer.OrdinalIgnoreCase);
            var hasCustom = visibleSet.Count > 0;

            _suppressColumnPersistence = true;
            try
            {
                foreach (var option in _columnOptions)
                {
                    var isVisible = !hasCustom || visibleSet.Contains(option.Key);
                    option.IsVisible = isVisible;
                    _columnVisibility[option.Key] = isVisible;
                }
            }
            finally
            {
                _suppressColumnPersistence = false;
            }
        }

        private async Task PersistColumnPreferencesAsync()
        {
            try
            {
                await _preferencesReady.Task.ConfigureAwait(false);
                var visibleColumns = _columnOptions
                    .Where(option => option.IsVisible)
                    .Select(option => option.Key)
                    .ToArray();

                var updated = _preferences with
                {
                    Library = new LibraryPreferences
                    {
                        VisibleColumns = visibleColumns,
                        LastUpdatedUtc = DateTimeOffset.UtcNow
                    }
                };

                _preferences = updated;
                await _preferencesStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LibraryViewModel] Failed to persist column preferences: {ex}");
            }
        }

        private LibrarySearchResult? GetSelection(LibrarySearchResult? candidate, bool updateSelection)
        {
            if (candidate is not null && updateSelection && !ReferenceEquals(Results.Selected, candidate))
            {
                Results.Selected = candidate;
            }

            return candidate ?? Results.Selected;
        }

        private bool CanOpenEntry(LibrarySearchResult? result) => GetSelection(result, updateSelection: false) is not null;

        [RelayCommand(CanExecute = nameof(CanOpenEntry))]
        private async Task OpenEntryAsync(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: true);
            if (target is null)
                return;

            try
            {
                await _documentService.OpenEntryAsync(target.Entry).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open entry:\n{ex.Message}",
                    "Open Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanOpenContainingFolder(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: false);
            if (target?.Entry is null)
                return false;

            return !string.IsNullOrWhiteSpace(target.Entry.MainFilePath);
        }

        [RelayCommand(CanExecute = nameof(CanOpenContainingFolder))]
        private void OpenContainingFolder(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: true);
            var entry = target?.Entry;
            if (entry is null)
                return;

            if (string.IsNullOrWhiteSpace(entry.MainFilePath))
            {
                System.Windows.MessageBox.Show(
                    "Entry does not have an associated file.",
                    "Open Containing Folder",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            try
            {
                var absolute = _workspace.GetAbsolutePath(entry.MainFilePath);
                _fileExplorer.RevealInExplorer(absolute);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open containing folder:\n{ex.Message}",
                    "Open Containing Folder",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanCopyMetadata(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: false);
            return target?.Entry is not null;
        }

        [RelayCommand(CanExecute = nameof(CanCopyMetadata))]
        private void CopyMetadata(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: true);
            var entry = target?.Entry;
            if (entry is null)
                return;

            var components = new List<string>();

            if (!string.IsNullOrWhiteSpace(entry.ShortTitle))
                components.Add(entry.ShortTitle!);
            else if (!string.IsNullOrWhiteSpace(entry.DisplayName))
                components.Add(entry.DisplayName!);
            else
                components.Add(BibliographyHelper.GenerateShortTitle(entry.Title, entry.Authors, entry.Source, entry.Year));

            if (!string.IsNullOrWhiteSpace(entry.Doi))
            {
                var doi = entry.Doi!.Trim();
                if (!doi.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    doi = $"https://doi.org/{doi}";
                components.Add(doi);
            }

            try
            {
                var payload = string.Join(Environment.NewLine, components.Where(static part => !string.IsNullOrWhiteSpace(part)));
                _clipboard.SetText(payload);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to copy metadata:\n{ex.Message}",
                    "Copy Citation / DOI",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanCopyWorkspacePath(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: false);
            var entry = target?.Entry;
            return entry is not null && !string.IsNullOrWhiteSpace(entry.MainFilePath);
        }

        [RelayCommand(CanExecute = nameof(CanCopyWorkspacePath))]
        private void CopyWorkspacePath(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: true);
            var entry = target?.Entry;
            if (entry is null)
                return;

            if (string.IsNullOrWhiteSpace(entry.MainFilePath))
            {
                System.Windows.MessageBox.Show(
                    "Entry does not have a stored file path.",
                    "Copy Workspace Path",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            try
            {
                var absolute = _workspace.GetAbsolutePath(entry.MainFilePath);
                _clipboard.SetText(absolute);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to copy workspace path:\n{ex.Message}",
                    "Copy Workspace Path",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanEditEntry(LibrarySearchResult? result) => GetSelection(result, updateSelection: false) is not null;

        [RelayCommand(CanExecute = nameof(CanEditEntry))]
        private async Task EditEntryAsync(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: true);
            if (target is null)
                return;

            await Results.EditEntryAsync(target).ConfigureAwait(false);
        }

        private bool CanOpenDataExtraction(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: false);
            var entry = target?.Entry;
            return entry is not null && EntryHasPdf(entry);
        }

        [RelayCommand(CanExecute = nameof(CanOpenDataExtraction))]
        private async Task OpenDataExtractionAsync(LibrarySearchResult? result)
        {
            var target = GetSelection(result, updateSelection: true);
            var entry = target?.Entry;
            if (entry is null)
                return;

            try
            {
                await _dataExtractionLauncher(entry, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open data extraction playground:\n{ex.Message}",
                    "Data extraction",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private static bool EntryHasPdf(Entry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.MainFilePath) &&
                string.Equals(Path.GetExtension(entry.MainFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (entry.Attachments is null)
                return false;

            foreach (var attachment in entry.Attachments)
            {
                if (attachment is null)
                    continue;

                if (string.IsNullOrWhiteSpace(attachment.RelativePath))
                    continue;

                if (string.Equals(Path.GetExtension(attachment.RelativePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}

