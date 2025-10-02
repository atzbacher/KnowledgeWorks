using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.Library.Search;
using LM.App.Wpf.ViewModels.Library.SavedSearches;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Search;
using LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library
{
    /// <summary>
    /// Holds search state, preset persistence, and navigation metadata for the Library view.
    /// </summary>
    public sealed partial class LibraryFiltersViewModel : ViewModelBase
    {
        private readonly LibraryFilterPresetStore _presetStore;
        private readonly ILibraryPresetPrompt _presetPrompt;
        private readonly IEntryStore _store;
        private readonly IWorkSpaceService _workspace;
        private readonly SemaphoreSlim _presetLock = new(1, 1);
        private readonly SemaphoreSlim _navigationLock = new(1, 1);
        private bool _initialized;
        private bool _suppressLeftPanelSync;
        private bool _suppressRightPanelSync;
        private bool _suppressNavigationRefresh;
        private double _lastLeftPanelWidth = DefaultLeftPanelWidth;
        private double _lastRightPanelWidth = DefaultRightPanelWidth;
        private static readonly System.StringComparer TagComparer = System.StringComparer.OrdinalIgnoreCase;
        private static readonly LibraryInlineDirectiveParser InlineDirectiveParser = new();

        private const double DefaultLeftPanelWidth = 310;
        private const double DefaultRightPanelWidth = 360;
        private const double CollapsedPanelWidth = 0;
        private bool _fromDirectiveActive;
        private bool _toDirectiveActive;

        [ObservableProperty]
        private bool useFullTextSearch;

        [ObservableProperty]
        private string? unifiedQuery;

        [ObservableProperty]
        private System.Windows.GridLength leftPanelWidth = new System.Windows.GridLength(DefaultLeftPanelWidth);

        [ObservableProperty]
        private System.Windows.GridLength rightPanelWidth = new System.Windows.GridLength(DefaultRightPanelWidth);

        [ObservableProperty]
        private bool isLeftPanelCollapsed;

        [ObservableProperty]
        private bool isRightPanelCollapsed;

        [ObservableProperty]
        private string? fullTextQuery;

        [ObservableProperty]
        private bool fullTextInTitle = true;

        [ObservableProperty]
        private bool fullTextInAbstract = true;

        [ObservableProperty]
        private bool fullTextInContent = true;

        [ObservableProperty]
        private IReadOnlyList<LibraryPresetSummary> savedPresets = Array.Empty<LibraryPresetSummary>();

        [ObservableProperty]
        private System.DateTime? dateFrom;

        [ObservableProperty]
        private System.DateTime? dateTo;

        [ObservableProperty]
        private LibrarySortOption selectedSort = LibrarySortOptions.NewestFirst;

        public ObservableCollection<LibraryNavigationNodeViewModel> NavigationRoots { get; } = new();

        public ObservableCollection<string> SelectedTags { get; } = new();

        public IReadOnlyList<LibrarySortOption> SortOptions => LibrarySortOptions.All;

        public bool HasSavedPresets => SavedPresets.Count > 0;

        public SavedSearchTreeViewModel SavedSearches { get; }

        public IReadOnlyList<string> KeywordTokens { get; } = LibrarySearchFieldMap.GetDisplayTokens();

        public string KeywordTooltip { get; } = BuildKeywordTooltip();

        public IRelayCommand ClearCommand { get; }

        public IRelayCommand<string?> RemoveTagCommand { get; }

        public LibraryFiltersViewModel(LibraryFilterPresetStore presetStore,
                                       ILibraryPresetPrompt presetPrompt,
                                       IEntryStore store,
                                       IWorkSpaceService workspace)
        {
            _presetStore = presetStore ?? throw new ArgumentNullException(nameof(presetStore));
            _presetPrompt = presetPrompt ?? throw new ArgumentNullException(nameof(presetPrompt));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            SavedSearches = new SavedSearchTreeViewModel(_presetStore, _presetPrompt);
            SavedSearches.TreeChanged += OnSavedSearchTreeChanged;

            RemoveTagCommand = new RelayCommand<string?>(RemoveTag, CanRemoveTag);
            SelectedTags.CollectionChanged += OnSelectedTagsCollectionChanged;

            ClearCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Clear);
        }

        private static string BuildKeywordTooltip()
        {
            var tokens = LibrarySearchFieldMap.GetDisplayTokens();
            if (tokens.Count == 0)
            {
                return "Supported keywords: (none)";
            }

            var formatted = tokens.Select(static token => string.Concat(token, ":"));
            return "Supported keywords: " + string.Join(", ", formatted);
        }

        private void OnSelectedTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RemoveTagCommand.NotifyCanExecuteChanged();
            Trace.WriteLine($"[LibraryFiltersViewModel] Selected tags updated via {e.Action}; total tags: {SelectedTags.Count}");
        }

        private bool CanRemoveTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var normalized = tag.Trim();
            return SelectedTags.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase));
        }

        private void RemoveTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            var normalized = tag.Trim();
            var removed = false;
            for (var i = SelectedTags.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(SelectedTags[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SelectedTags.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                Trace.WriteLine($"[LibraryFiltersViewModel] Removed tag '{normalized}'.");
            }
        }

        private void ReplaceTags(IEnumerable<string> tags)
        {
            SelectedTags.Clear();

            if (tags is null)
            {
                return;
            }

            var unique = new HashSet<string>(TagComparer);
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                var normalized = tag.Trim();
                if (unique.Add(normalized))
                {
                    SelectedTags.Add(normalized);
                }
            }

            Trace.WriteLine($"[LibraryFiltersViewModel] Loaded {SelectedTags.Count} tag filter(s).");
        }

        private static LibrarySortOption ResolveSortOption(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return LibrarySortOptions.NewestFirst;
            }

            var resolved = LibrarySortOptions.All.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase));
            return resolved ?? LibrarySortOptions.NewestFirst;
        }

        partial void OnDateFromChanged(System.DateTime? value)
        {
            if (value.HasValue && DateTo.HasValue && value.Value > DateTo.Value)
            {
                Trace.WriteLine("[LibraryFiltersViewModel] Adjusting DateTo to match new DateFrom value.");
                DateTo = value;
            }
        }

        partial void OnDateToChanged(System.DateTime? value)
        {
            if (value.HasValue && DateFrom.HasValue && value.Value < DateFrom.Value)
            {
                Trace.WriteLine("[LibraryFiltersViewModel] Adjusting DateFrom to match new DateTo value.");
                DateFrom = value;
            }
        }

        partial void OnLeftPanelWidthChanged(System.Windows.GridLength value)
        {
            if (_suppressLeftPanelSync)
            {
                return;
            }

            var numeric = value.IsAbsolute ? value.Value : DefaultLeftPanelWidth;
            if (numeric > 1)
            {
                _lastLeftPanelWidth = numeric;
                if (IsLeftPanelCollapsed)
                {
                    _suppressLeftPanelSync = true;
                    IsLeftPanelCollapsed = false;
                    _suppressLeftPanelSync = false;
                }
            }
            else if (!IsLeftPanelCollapsed)
            {
                _suppressLeftPanelSync = true;
                IsLeftPanelCollapsed = true;
                _suppressLeftPanelSync = false;
            }
        }

        partial void OnRightPanelWidthChanged(System.Windows.GridLength value)
        {
            if (_suppressRightPanelSync)
            {
                return;
            }

            var numeric = value.IsAbsolute ? value.Value : DefaultRightPanelWidth;
            if (numeric > 1)
            {
                _lastRightPanelWidth = numeric;
                if (IsRightPanelCollapsed)
                {
                    _suppressRightPanelSync = true;
                    IsRightPanelCollapsed = false;
                    _suppressRightPanelSync = false;
                }
            }
            else if (!IsRightPanelCollapsed)
            {
                _suppressRightPanelSync = true;
                IsRightPanelCollapsed = true;
                _suppressRightPanelSync = false;
            }
        }

        partial void OnIsLeftPanelCollapsedChanged(bool value)
        {
            if (_suppressLeftPanelSync)
            {
                return;
            }

            _suppressLeftPanelSync = true;
            try
            {
                if (value)
                {
                    if (LeftPanelWidth.Value > 1)
                    {
                        _lastLeftPanelWidth = LeftPanelWidth.Value;
                    }

                    LeftPanelWidth = new System.Windows.GridLength(CollapsedPanelWidth);
                }
                else
                {
                    var width = _lastLeftPanelWidth > 1 ? _lastLeftPanelWidth : DefaultLeftPanelWidth;
                    LeftPanelWidth = new System.Windows.GridLength(width);
                }
            }
            finally
            {
                _suppressLeftPanelSync = false;
            }
        }

        partial void OnIsRightPanelCollapsedChanged(bool value)
        {
            if (_suppressRightPanelSync)
            {
                return;
            }

            _suppressRightPanelSync = true;
            try
            {
                if (value)
                {
                    if (RightPanelWidth.Value > 1)
                    {
                        _lastRightPanelWidth = RightPanelWidth.Value;
                    }

                    RightPanelWidth = new System.Windows.GridLength(CollapsedPanelWidth);
                }
                else
                {
                    var width = _lastRightPanelWidth > 1 ? _lastRightPanelWidth : DefaultRightPanelWidth;
                    RightPanelWidth = new System.Windows.GridLength(width);
                }
            }
            finally
            {
                _suppressRightPanelSync = false;
            }
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                await RefreshSavedPresetsAsync(ct).ConfigureAwait(false);
                _initialized = true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Debug.WriteLine($"[LibraryFiltersViewModel] Failed to initialize: {ex}");
            }
        }

        public void Clear()
        {
            Trace.WriteLine("[LibraryFiltersViewModel] Clearing all filter values.");
            UseFullTextSearch = false;
            UnifiedQuery = string.Empty;
            FullTextQuery = string.Empty;
            FullTextInTitle = true;
            FullTextInAbstract = true;
            FullTextInContent = true;
            DateFrom = null;
            DateTo = null;
            SelectedSort = LibrarySortOptions.NewestFirst;
            SelectedTags.Clear();
            _fromDirectiveActive = false;
            _toDirectiveActive = false;
        }

        [RelayCommand]
        private void ToggleLeftPanel()
        {
            IsLeftPanelCollapsed = !IsLeftPanelCollapsed;
        }

        [RelayCommand]
        private void ToggleRightPanel()
        {
            IsRightPanelCollapsed = !IsRightPanelCollapsed;
        }

        internal LibraryInlineDirectiveResult ApplyInlineSearchDirectives()
        {
            var query = UnifiedQuery ?? string.Empty;
            var directives = InlineDirectiveParser.Parse(query);

            var normalizedFullText = directives.FullTextQuery ?? string.Empty;
            var hasFullText = directives.HasFullTextDirective && !string.IsNullOrWhiteSpace(normalizedFullText);

            if (!string.Equals(FullTextQuery ?? string.Empty, normalizedFullText, StringComparison.Ordinal))
            {
                FullTextQuery = normalizedFullText;
                Trace.WriteLine($"[LibraryFiltersViewModel] Inline FULLTEXT query set to '{normalizedFullText}'.");
            }

            if (UseFullTextSearch != hasFullText)
            {
                Trace.WriteLine(hasFullText
                    ? "[LibraryFiltersViewModel] Inline FULLTEXT directive detected; switching to full-text search."
                    : "[LibraryFiltersViewModel] Inline FULLTEXT directive missing; using metadata search.");
                UseFullTextSearch = hasFullText;
            }

            if (directives.HasFromDirective)
            {
                _fromDirectiveActive = true;
                if (directives.FromDate.HasValue)
                {
                    if (DateFrom != directives.FromDate)
                    {
                        DateFrom = directives.FromDate;
                        Trace.WriteLine($"[LibraryFiltersViewModel] Applied inline FROM directive: {DateFrom:yyyy-MM-dd}.");
                    }
                }
                else if (DateFrom is not null)
                {
                    DateFrom = null;
                    Trace.WriteLine("[LibraryFiltersViewModel] FROM directive present but invalid; cleared date filter.");
                }
            }
            else if (_fromDirectiveActive)
            {
                if (DateFrom is not null)
                {
                    DateFrom = null;
                    Trace.WriteLine("[LibraryFiltersViewModel] Removed inline FROM directive; cleared date filter.");
                }

                _fromDirectiveActive = false;
            }

            if (directives.HasToDirective)
            {
                _toDirectiveActive = true;
                if (directives.ToDate.HasValue)
                {
                    if (DateTo != directives.ToDate)
                    {
                        DateTo = directives.ToDate;
                        Trace.WriteLine($"[LibraryFiltersViewModel] Applied inline TO directive: {DateTo:yyyy-MM-dd}.");
                    }
                }
                else if (DateTo is not null)
                {
                    DateTo = null;
                    Trace.WriteLine("[LibraryFiltersViewModel] TO directive present but invalid; cleared date filter.");
                }
            }
            else if (_toDirectiveActive)
            {
                if (DateTo is not null)
                {
                    DateTo = null;
                    Trace.WriteLine("[LibraryFiltersViewModel] Removed inline TO directive; cleared date filter.");
                }

                _toDirectiveActive = false;
            }

            return directives;
        }

        public string GetNormalizedFullTextQuery()
        {
            return (FullTextQuery ?? string.Empty).Trim();
        }

        public FullTextSearchQuery BuildFullTextQuery(string normalizedQuery)
        {
            if (normalizedQuery is null)
            {
                throw new ArgumentNullException(nameof(normalizedQuery));
            }

            var fields = FullTextSearchField.None;
            if (FullTextInTitle)
            {
                fields |= FullTextSearchField.Title;
            }
            if (FullTextInAbstract)
            {
                fields |= FullTextSearchField.Abstract;
            }
            if (FullTextInContent)
            {
                fields |= FullTextSearchField.Content;
            }

            if (fields == FullTextSearchField.None)
            {
                fields = FullTextSearchField.Title | FullTextSearchField.Abstract | FullTextSearchField.Content;
            }

            return new FullTextSearchQuery
            {
                Text = normalizedQuery,
                Fields = fields
            };
        }

        public LibraryFilterState CaptureState()
            => new()
            {
                UseFullTextSearch = UseFullTextSearch,
                UnifiedQuery = UnifiedQuery,
                FullTextQuery = FullTextQuery,
                FullTextInTitle = FullTextInTitle,
                FullTextInAbstract = FullTextInAbstract,
                FullTextInContent = FullTextInContent,
                DateFrom = DateFrom?.Date,
                DateTo = DateTo?.Date,
                SortKey = SelectedSort?.Key,
                Tags = SelectedTags
                    .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(static tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

        public void ApplyState(LibraryFilterState state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            UseFullTextSearch = state.UseFullTextSearch;
            UnifiedQuery = state.UnifiedQuery;
            FullTextQuery = state.FullTextQuery;
            FullTextInTitle = state.FullTextInTitle;
            FullTextInAbstract = state.FullTextInAbstract;
            FullTextInContent = state.FullTextInContent;
            DateFrom = state.DateFrom?.Date;
            DateTo = state.DateTo?.Date;
            SelectedSort = ResolveSortOption(state.SortKey);
            ReplaceTags(state.Tags ?? Array.Empty<string>());
            Trace.WriteLine("[LibraryFiltersViewModel] Applied preset state to filters.");
        }

        [RelayCommand]
        private async Task SavePresetAsync()
        {
            try
            {
                var existing = await _presetStore.ListPresetsAsync().ConfigureAwait(false);
                var defaultName = BuildDefaultPresetName(existing);
                var context = new LibraryPresetSaveContext(defaultName, existing.Select(p => p.Name).ToArray());
                var prompt = await _presetPrompt.RequestSaveAsync(context).ConfigureAwait(false);
                if (prompt is null)
                {
                    return;
                }

                var name = prompt.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                if (existing.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    var result = await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                        $"Preset \"{name}\" already exists. Overwrite?",
                        "Save Library Preset",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question)).ConfigureAwait(false);
                    if (result != System.Windows.MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                var preset = new LibraryFilterPreset
                {
                    Name = name,
                    State = CaptureState()
                };

                await _presetStore.SavePresetAsync(preset, LibraryPresetFolder.RootId).ConfigureAwait(false);
                await RefreshSavedPresetsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                    $"Failed to save preset:\n{ex.Message}",
                    "Save Library Preset",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error)).ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private async Task LoadPresetAsync()
        {
            try
            {
                var presets = await _presetStore.ListPresetsAsync().ConfigureAwait(false);
                if (presets.Count == 0)
                {
                    await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                        "No saved searches yet.",
                        "Load Saved Search",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information)).ConfigureAwait(false);
                    return;
                }

                var summaries = presets.Select(p => new LibraryPresetSummary(p.Id, p.Name, p.SavedUtc)).ToArray();
                var result = await _presetPrompt.RequestSelectionAsync(
                    new LibraryPresetSelectionContext(summaries, AllowLoad: true, "Load Saved Search")).ConfigureAwait(false);
                if (result is null)
                {
                    return;
                }

                await DeletePresetsAsync(result.DeletedPresetIds).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(result.SelectedPresetId))
                {
                    return;
                }

                var summary = summaries.FirstOrDefault(s => string.Equals(s.Id, result.SelectedPresetId, StringComparison.Ordinal));
                if (summary is not null)
                {
                    await ApplyPresetAsync(summary).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                    $"Failed to load preset:\n{ex.Message}",
                    "Load Saved Search",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error)).ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private async Task ManagePresetsAsync()
        {
            try
            {
                var presets = await _presetStore.ListPresetsAsync().ConfigureAwait(false);
                if (presets.Count == 0)
                {
                    await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                        "No saved searches yet.",
                        "Manage Saved Searches",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information)).ConfigureAwait(false);
                    return;
                }

                var summaries = presets.Select(p => new LibraryPresetSummary(p.Id, p.Name, p.SavedUtc)).ToArray();
                var result = await _presetPrompt.RequestSelectionAsync(
                    new LibraryPresetSelectionContext(summaries, AllowLoad: false, "Manage Saved Searches")).ConfigureAwait(false);
                if (result is null)
                {
                    return;
                }

                await DeletePresetsAsync(result.DeletedPresetIds).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                    $"Failed to manage presets:\n{ex.Message}",
                    "Manage Saved Searches",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error)).ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private Task ApplyPresetCommandAsync(LibraryPresetSummary summary)
            => ApplyPresetAsync(summary);

        public async Task<bool> ApplyPresetAsync(LibraryPresetSummary summary, CancellationToken ct = default)
        {
            if (summary is null)
            {
                return false;
            }

            try
            {
                var preset = await _presetStore.TryGetPresetByIdAsync(summary.Id, ct).ConfigureAwait(false)
                             ?? await _presetStore.TryGetPresetAsync(summary.Name, ct).ConfigureAwait(false);
                if (preset is null)
                {
                    await RefreshSavedPresetsAsync(ct).ConfigureAwait(false);
                    await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                        $"Saved search \"{summary.Name}\" could not be found.",
                        "Load Saved Search",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning)).ConfigureAwait(false);
                    return false;
                }

                await InvokeOnDispatcherAsync(() => ApplyState(preset.State)).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                await InvokeOnDispatcherAsync(() => System.Windows.MessageBox.Show(
                    $"Failed to load preset:\n{ex.Message}",
                    "Load Saved Search",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error)).ConfigureAwait(false);
                return false;
            }
        }

        public async Task RefreshNavigationAsync(CancellationToken ct = default)
        {
            await _navigationLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var nodes = new List<LibraryNavigationNodeViewModel>();
                var saved = BuildSavedSearchNode();
                if (saved.HasChildren)
                {
                    nodes.Add(saved);
                }

                var litNodes = await BuildLitSearchNodesAsync(ct).ConfigureAwait(false);
                if (litNodes.Count > 0)
                {
                    nodes.AddRange(litNodes);
                }

                await InvokeOnDispatcherAsync(() =>
                {
                    NavigationRoots.Clear();
                    foreach (var node in nodes)
                    {
                        NavigationRoots.Add(node);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                _navigationLock.Release();
            }
        }

        private LibraryNavigationNodeViewModel BuildSavedSearchNode()
        {
            var root = new LibraryNavigationNodeViewModel("Saved Searches", LibraryNavigationNodeKind.Category);
            foreach (var preset in SavedPresets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                var child = new LibraryNavigationNodeViewModel(preset.Name, LibraryNavigationNodeKind.SavedSearch)
                {
                    Payload = new LibrarySavedSearchPayload(preset)
                };
                child.Subtitle = $"Saved {preset.SavedUtc:u}";
                root.Children.Add(child);
            }

            return root;
        }

        private async Task<List<LibraryNavigationNodeViewModel>> BuildLitSearchNodesAsync(CancellationToken ct)
        {
            var nodes = new List<LibraryNavigationNodeViewModel>();
            try
            {
                var workspaceRoot = _workspace.GetWorkspaceRoot();
                await foreach (var entry in _store.EnumerateAsync(ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
                    {
                        continue;
                    }

                    var isLitSearch = entry.Type == EntryType.LitSearch
                        || string.Equals(entry.Source, "LitSearch", StringComparison.OrdinalIgnoreCase);
                    if (!isLitSearch)
                    {
                        continue;
                    }

                    var hookPath = FindLitSearchHookPath(workspaceRoot, entry.Id);
                    if (hookPath is null)
                    {
                        continue;
                    }

                    try
                    {
                        var json = await File.ReadAllTextAsync(hookPath, ct).ConfigureAwait(false);
                        var hook = JsonSerializer.Deserialize<LitSearchHook>(json, JsonStd.Options);
                        if (hook is null)
                        {
                            continue;
                        }

                        var title = string.IsNullOrWhiteSpace(hook.Title) ? entry.Title ?? entry.Id : hook.Title;
                        var entryNode = new LibraryNavigationNodeViewModel(title!, LibraryNavigationNodeKind.LitSearchEntry)
                        {
                            Payload = new LibraryLitSearchEntryPayload(entry.Id!, hookPath, title!, hook.Query)
                        };

                        foreach (var run in hook.Runs.OrderByDescending(r => r.RunUtc))
                        {
                            if (string.IsNullOrWhiteSpace(run.RunId))
                            {
                                continue;
                            }

                            var label = $"{run.RunUtc:u} ({run.TotalHits} hits)";
                            var runNode = new LibraryNavigationNodeViewModel(label, LibraryNavigationNodeKind.LitSearchRun)
                            {
                                Payload = new LibraryLitSearchRunPayload(entry.Id!, run.RunId, ResolveCheckedEntriesPath(workspaceRoot, run.CheckedEntryIdsPath), label)
                            };
                            entryNode.Children.Add(runNode);
                        }

                        nodes.Add(entryNode);
                    }
                    catch
                    {
                        // ignore malformed litsearch entries
                    }
                }
            }

            catch
            {
                // workspace not ready or enumeration failed; ignore for now
            }

            if (nodes.Count == 0)
            {
                return nodes;
            }

            nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var root = new LibraryNavigationNodeViewModel("LitSearch", LibraryNavigationNodeKind.Category);
            foreach (var node in nodes)
            {
                root.Children.Add(node);
            }

            return new List<LibraryNavigationNodeViewModel> { root };
        }

        private static string? FindLitSearchHookPath(string workspaceRoot, string entryId)
        {
            var candidates = new[]
            {
                Path.Combine(workspaceRoot, "entries", entryId, "hooks", "litsearch.json"),
                Path.Combine(workspaceRoot, "entries", entryId, "spokes", "litsearch", "litsearch.json"),
                Path.Combine(workspaceRoot, "entries", entryId, "litsearch", "litsearch.json")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string? ResolveCheckedEntriesPath(string workspaceRoot, string? relative)
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                return null;
            }

            var normalized = relative.Replace('/', Path.DirectorySeparatorChar);
            var combined = Path.Combine(workspaceRoot, normalized);
            return File.Exists(combined) ? combined : null;
        }

        private async Task DeletePresetsAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
        {
            if (ids is null || ids.Count == 0)
            {
                return;
            }

            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                await _presetStore.DeletePresetAsync(id, ct).ConfigureAwait(false);
            }

            await RefreshSavedPresetsAsync(ct).ConfigureAwait(false);
        }

        private string BuildDefaultPresetName(IReadOnlyList<LibraryFilterPreset> existing)
        {
            var query = UnifiedQuery;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var trimmed = query.Trim();
                if (trimmed.Length > 40)
                {
                    trimmed = trimmed[..40];
                }

                return trimmed;
            }

            var index = existing.Count + 1;
            string name;
            do
            {
                name = $"Saved search {index}";
                index++;
            } while (existing.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

            return name;
        }

        private async Task RefreshSavedPresetsAsync(CancellationToken ct = default)
        {
            await _presetLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _suppressNavigationRefresh = true;
                await SavedSearches.RefreshAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _suppressNavigationRefresh = false;
                _presetLock.Release();
            }

            if (!ct.IsCancellationRequested)
            {
                await RefreshNavigationAsync(ct).ConfigureAwait(false);
            }
        }

        private void OnSavedSearchTreeChanged(object? sender, SavedSearchTreeChangedEventArgs e)
        {
            var _ = InvokeOnDispatcherAsync(() => SavedPresets = e.Presets);
            if (!_suppressNavigationRefresh)
            {
                Trace.WriteLine($"[LibraryFiltersViewModel] Saved search tree changed; scheduling navigation refresh for {e.Presets.Count} preset(s).");
                _ = RefreshNavigationAsync();
            }
        }

        private static Task InvokeOnDispatcherAsync(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                action();
                return Task.CompletedTask;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        private static Task<TResult> InvokeOnDispatcherAsync<TResult>(Func<TResult> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return Task.FromResult(callback());
            }

            if (dispatcher.CheckAccess())
            {
                return Task.FromResult(callback());
            }

            return dispatcher.InvokeAsync(callback).Task;
        }

        partial void OnSavedPresetsChanged(IReadOnlyList<LibraryPresetSummary> value)
            => OnPropertyChanged(nameof(HasSavedPresets));
    }
}
