using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Core.Models.Search;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>
    /// Fielded metadata search against the JSON-backed entry store for the current workspace.
    /// </summary>
    public sealed class LibraryViewModel : ViewModelBase
    {
        private readonly IEntryStore _store;
        private readonly IFullTextSearchService _fullTextSearch;
        private readonly IWorkSpaceService _ws;
        private LibrarySearchResult? _selected;
        private string? _sourceContains;
        private string? _internalIdContains;
        private string? _doiContains;
        private string? _pmidContains;
        private string? _nctContains;
        private string? _addedByContains;
        private DateTime? _addedOnFrom;
        private DateTime? _addedOnTo;
        private readonly LibraryFilterPresetStore _presetStore;
        private readonly ILibraryPresetPrompt _presetPrompt;
        private bool _useFullTextSearch;
        private string? _fullTextQuery;
        private bool _fullTextInTitle = true;
        private bool _fullTextInAbstract = true;
        private bool _fullTextInContent = true;
        private bool _resultsAreFullText;

        public LibraryViewModel(IEntryStore store, IFullTextSearchService fullTextSearch, IWorkSpaceService ws, LibraryFilterPresetStore presetStore, ILibraryPresetPrompt presetPrompt)
        {
            _store = store;
            _fullTextSearch = fullTextSearch ?? throw new ArgumentNullException(nameof(fullTextSearch));
            _ws = ws;
            _presetStore = presetStore ?? throw new ArgumentNullException(nameof(presetStore));
            _presetPrompt = presetPrompt ?? throw new ArgumentNullException(nameof(presetPrompt));

            SearchCommand = new AsyncRelayCommand(SearchAsync);
            ClearCommand  = new RelayCommand(_ => ClearFilters());
            OpenCommand   = new RelayCommand(_ => OpenSelected(), _ => Selected?.Entry is not null);
            SavePresetCommand = new AsyncRelayCommand(SavePresetAsync);
            LoadPresetCommand = new AsyncRelayCommand(LoadPresetAsync);
            ManagePresetsCommand = new AsyncRelayCommand(ManagePresetsAsync);

            Types = Enum.GetValues(typeof(EntryType)).Cast<EntryType>().ToArray();
        }

        // Filters
        public string? TitleContains { get; set; }
        public string? AuthorContains { get; set; } 
        public string? TagsCsv { get; set; }
        public bool? IsInternal { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string? SourceContains
        {
            get => _sourceContains;
            set
            {
                if (_sourceContains != value)
                {
                    _sourceContains = value;
                    OnPropertyChanged();
                }
            }
        }
        public string? InternalIdContains
        {
            get => _internalIdContains;
            set
            {
                if (_internalIdContains != value)
                {
                    _internalIdContains = value;
                    OnPropertyChanged();
                }
            }
        }
        public string? DoiContains
        {
            get => _doiContains;
            set
            {
                if (_doiContains != value)
                {
                    _doiContains = value;
                    OnPropertyChanged();
                }
            }
        }
        public string? PmidContains
        {
            get => _pmidContains;
            set
            {
                if (_pmidContains != value)
                {
                    _pmidContains = value;
                    OnPropertyChanged();
                }
            }
        }
        public string? NctContains
        {
            get => _nctContains;
            set
            {
                if (_nctContains != value)
                {
                    _nctContains = value;
                    OnPropertyChanged();
                }
            }
        }
        public string? AddedByContains
        {
            get => _addedByContains;
            set
            {
                if (_addedByContains != value)
                {
                    _addedByContains = value;
                    OnPropertyChanged();
                }
            }
        }
        public DateTime? AddedOnFrom
        {
            get => _addedOnFrom;
            set
            {
                if (_addedOnFrom != value)
                {
                    _addedOnFrom = value;
                    OnPropertyChanged();
                }
            }
        }
        public DateTime? AddedOnTo
        {
            get => _addedOnTo;
            set
            {
                if (_addedOnTo != value)
                {
                    _addedOnTo = value;
                    OnPropertyChanged();
                }
            }
        }
        public EntryType[] Types { get; }
        public bool TypePublication { get; set; } = true;
        public bool TypePresentation { get; set; } = true;
        public bool TypeWhitePaper { get; set; } = true;
        public bool TypeSlideDeck { get; set; } = true;
        public bool TypeReport { get; set; } = true;
        public bool TypeOther { get; set; } = true;

        public bool UseFullTextSearch
        {
            get => _useFullTextSearch;
            set
            {
                if (_useFullTextSearch != value)
                {
                    _useFullTextSearch = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMetadataSearch));
                    if (!value)
                        ResultsAreFullText = false;
                }
            }
        }

        public bool IsMetadataSearch => !UseFullTextSearch;

        public string? FullTextQuery
        {
            get => _fullTextQuery;
            set
            {
                if (_fullTextQuery != value)
                {
                    _fullTextQuery = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FullTextInTitle
        {
            get => _fullTextInTitle;
            set
            {
                if (_fullTextInTitle != value)
                {
                    _fullTextInTitle = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FullTextInAbstract
        {
            get => _fullTextInAbstract;
            set
            {
                if (_fullTextInAbstract != value)
                {
                    _fullTextInAbstract = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FullTextInContent
        {
            get => _fullTextInContent;
            set
            {
                if (_fullTextInContent != value)
                {
                    _fullTextInContent = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ResultsAreFullText
        {
            get => _resultsAreFullText;
            private set
            {
                if (_resultsAreFullText != value)
                {
                    _resultsAreFullText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<LibrarySearchResult> Results { get; } = new();

        public LibrarySearchResult? Selected
        {
            get => _selected;
            set
            {
                if (!ReferenceEquals(_selected, value))
                {
                    _selected = value;
                    OnPropertyChanged();
                    (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SavePresetCommand { get; }
        public ICommand LoadPresetCommand { get; }
        public ICommand ManagePresetsCommand { get; }

        private EntryType[] BuildTypeFilter()
        {
            var list = new List<EntryType>();
            if (TypePublication) list.Add(EntryType.Publication);
            if (TypePresentation) list.Add(EntryType.Presentation);
            if (TypeWhitePaper) list.Add(EntryType.WhitePaper);
            if (TypeSlideDeck) list.Add(EntryType.SlideDeck);
            if (TypeReport) list.Add(EntryType.Report);
            if (TypeOther) list.Add(EntryType.Other);
            return list.ToArray();
        }

        private async Task SearchAsync()
        {
            try
            {
                Results.Clear();
                if (UseFullTextSearch)
                    await RunFullTextSearchAsync();
                else
                    await RunMetadataSearchAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryViewModel] SearchAsync FAILED: {ex}");
                throw; // or swallow if you prefer
            }
        }

        private EntryType[]? GetSelectedTypesOrNull()
            => TypePublication || TypePresentation || TypeReport || TypeWhitePaper || TypeSlideDeck || TypeOther
                ? BuildTypeFilter()
                : null;

        private async Task RunMetadataSearchAsync()
        {
            static string? TrimOrNull(string? value)
                => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            static DateTime? ToUtcStartOfDay(DateTime? localDate)
            {
                if (!localDate.HasValue) return null;
                var local = DateTime.SpecifyKind(localDate.Value.Date, DateTimeKind.Local);
                return local.ToUniversalTime();
            }

            static DateTime? ToUtcEndOfDay(DateTime? localDate)
            {
                if (!localDate.HasValue) return null;
                var local = DateTime.SpecifyKind(localDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
                return local.ToUniversalTime();
            }

            var filter = new EntryFilter

            {
                TitleContains = TrimOrNull(TitleContains),
                AuthorContains = TrimOrNull(AuthorContains),
                TagsAny = string.IsNullOrWhiteSpace(TagsCsv)
                    ? new List<string>()
                    : TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
                TypesAny = GetSelectedTypesOrNull(),
                YearFrom = YearFrom,
                YearTo = YearTo,
                IsInternal = IsInternal.HasValue ? IsInternal : null,
                SourceContains = TrimOrNull(SourceContains),
                InternalIdContains = TrimOrNull(InternalIdContains),
                DoiContains = TrimOrNull(DoiContains),
                PmidContains = TrimOrNull(PmidContains),
                NctContains = TrimOrNull(NctContains),
                AddedByContains = TrimOrNull(AddedByContains),
                AddedOnFromUtc = ToUtcStartOfDay(AddedOnFrom),
                AddedOnToUtc = ToUtcEndOfDay(AddedOnTo)
            };

            Debug.WriteLine(
$"Filter: Title='{filter.TitleContains}', Author='{filter.AuthorContains}', " +
$"Tags=[{string.Join(",", filter.TagsAny ?? new List<string>())}], " +
$"Types=[{string.Join(",", filter.TypesAny ?? Array.Empty<EntryType>())}], " +
$"YearFrom={filter.YearFrom}, YearTo={filter.YearTo}, IsInternal={filter.IsInternal}");

            var rows = await _store.SearchAsync(filter);
            ResultsAreFullText = false;

            Debug.WriteLine($"[LibraryViewModel] Metadata search → {rows.Count} rows");

            foreach (var entry in rows)
            {
                PrepareEntry(entry);
                Results.Add(new LibrarySearchResult(entry, null, null));
            }

            if (rows.Count == 0)
                Debug.WriteLine("[LibraryViewModel] No entries matched metadata filter");
        }

        private async Task RunFullTextSearchAsync()
        {
            var trimmed = string.IsNullOrWhiteSpace(FullTextQuery) ? null : FullTextQuery!.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                ResultsAreFullText = false;
                return;
            }

            var query = new FullTextSearchQuery
            {
                Text = trimmed,
                Fields = BuildFullTextFieldMask(),
                YearFrom = YearFrom,
                YearTo = YearTo,
                IsInternal = IsInternal,
                TypesAny = GetSelectedTypesOrNull()
            };

            var hits = await _fullTextSearch.SearchAsync(query);
            ResultsAreFullText = true;

            Debug.WriteLine($"[LibraryViewModel] Full-text search → {hits.Count} hits");

            foreach (var hit in hits)
            {
                var entry = await _store.GetByIdAsync(hit.EntryId);
                if (entry is null)
                    continue;

                PrepareEntry(entry);
                Results.Add(new LibrarySearchResult(entry, hit.Score, hit.Highlight));
            }

            if (hits.Count == 0)
                Debug.WriteLine("[LibraryViewModel] Full-text search returned no matches");
        }

        private FullTextSearchField BuildFullTextFieldMask()
        {
            var mask = FullTextSearchField.None;
            if (FullTextInTitle) mask |= FullTextSearchField.Title;
            if (FullTextInAbstract) mask |= FullTextSearchField.Abstract;
            if (FullTextInContent) mask |= FullTextSearchField.Content;

            return mask == FullTextSearchField.None
                ? FullTextSearchField.Title | FullTextSearchField.Abstract | FullTextSearchField.Content
                : mask;
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




        private void ClearFilters()
        {
            UseFullTextSearch = false;
            FullTextQuery = null;
            FullTextInTitle = FullTextInAbstract = FullTextInContent = true;
            ResultsAreFullText = false;
            TitleContains = AuthorContains = TagsCsv = null;
            IsInternal = null;
            YearFrom = YearTo = null;
            SourceContains = null;
            InternalIdContains = null;
            DoiContains = null;
            PmidContains = null;
            NctContains = null;
            AddedByContains = null;
            AddedOnFrom = null;
            AddedOnTo = null;
            TypePublication = TypePresentation = TypeWhitePaper = TypeSlideDeck = TypeReport = TypeOther = true;
            NotifyFiltersChanged();
        }

        private void OpenSelected()
        {
            var entry = Selected?.Entry;
            if (entry is null) return;
            try
            {
                var abs = _ws.GetAbsolutePath(entry.MainFilePath);
                Process.Start(new ProcessStartInfo { FileName = abs, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open file:\n{ex.Message}", "Open Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task SavePresetAsync()
        {
            try
            {
                var existing = await _presetStore.ListPresetsAsync();
                var defaultName = BuildDefaultPresetName(existing);
                var context = new LibraryPresetSaveContext(defaultName, existing.Select(p => p.Name).ToArray());
                var prompt = await _presetPrompt.RequestSaveAsync(context);
                if (prompt is null)
                    return;

                var name = prompt.Name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return;

                if (existing.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    var confirm = System.Windows.MessageBox.Show(
                        $"Preset \"{name}\" already exists. Overwrite?",
                        "Save Library Preset",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    if (confirm != System.Windows.MessageBoxResult.Yes)
                        return;
                }

                var preset = new LibraryFilterPreset
                {
                    Name = name,
                    State = CaptureState()
                };

                await _presetStore.SavePresetAsync(preset);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save preset:\n{ex.Message}",
                    "Save Library Preset",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadPresetAsync()
        {
            try
            {
                var presets = await _presetStore.ListPresetsAsync();
                if (presets.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "No presets saved yet.",
                        "Load Library Preset",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var summaries = presets
                    .Select(p => new LibraryPresetSummary(p.Name, p.SavedUtc))
                    .ToArray();

                var result = await _presetPrompt.RequestSelectionAsync(
                    new LibraryPresetSelectionContext(summaries, AllowLoad: true, "Load Library Preset"));

                if (result is null)
                    return;

                await DeletePresetsAsync(result.DeletedPresetNames);

                if (string.IsNullOrWhiteSpace(result.SelectedPresetName))
                    return;

                var preset = await _presetStore.TryGetPresetAsync(result.SelectedPresetName!);
                if (preset is null)
                {
                    System.Windows.MessageBox.Show(
                        $"Preset \"{result.SelectedPresetName}\" could not be found.",
                        "Load Library Preset",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                ApplyPreset(preset.State);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load preset:\n{ex.Message}",
                    "Load Library Preset",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ManagePresetsAsync()
        {
            try
            {
                var presets = await _presetStore.ListPresetsAsync();
                if (presets.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "No presets saved yet.",
                        "Manage Library Presets",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var summaries = presets
                    .Select(p => new LibraryPresetSummary(p.Name, p.SavedUtc))
                    .ToArray();

                var result = await _presetPrompt.RequestSelectionAsync(
                    new LibraryPresetSelectionContext(summaries, AllowLoad: false, "Manage Library Presets"));

                if (result is null)
                    return;

                await DeletePresetsAsync(result.DeletedPresetNames);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to manage presets:\n{ex.Message}",
                    "Manage Library Presets",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task DeletePresetsAsync(IReadOnlyList<string> names)
        {
            if (names is null || names.Count == 0)
                return;

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                await _presetStore.DeletePresetAsync(name);
            }
        }

        private LibraryFilterState CaptureState()
            => new()
            {
                UseFullTextSearch = UseFullTextSearch,
                FullTextQuery = FullTextQuery,
                FullTextInTitle = FullTextInTitle,
                FullTextInAbstract = FullTextInAbstract,
                FullTextInContent = FullTextInContent,
                TitleContains = TitleContains,
                AuthorContains = AuthorContains,
                TagsCsv = TagsCsv,
                IsInternal = IsInternal,
                YearFrom = YearFrom,
                YearTo = YearTo,
                SourceContains = SourceContains,
                InternalIdContains = InternalIdContains,
                DoiContains = DoiContains,
                PmidContains = PmidContains,
                NctContains = NctContains,
                AddedByContains = AddedByContains,
                AddedOnFrom = AddedOnFrom,
                AddedOnTo = AddedOnTo,
                TypePublication = TypePublication,
                TypePresentation = TypePresentation,
                TypeWhitePaper = TypeWhitePaper,
                TypeSlideDeck = TypeSlideDeck,
                TypeReport = TypeReport,
                TypeOther = TypeOther
            };

        private void ApplyPreset(LibraryFilterState state)
        {
            UseFullTextSearch = state.UseFullTextSearch;
            FullTextQuery = state.FullTextQuery;
            FullTextInTitle = state.FullTextInTitle;
            FullTextInAbstract = state.FullTextInAbstract;
            FullTextInContent = state.FullTextInContent;
            TitleContains = state.TitleContains;
            AuthorContains = state.AuthorContains;
            TagsCsv = state.TagsCsv;
            IsInternal = state.IsInternal;
            YearFrom = state.YearFrom;
            YearTo = state.YearTo;
            SourceContains = state.SourceContains;
            InternalIdContains = state.InternalIdContains;
            DoiContains = state.DoiContains;
            PmidContains = state.PmidContains;
            NctContains = state.NctContains;
            AddedByContains = state.AddedByContains;
            AddedOnFrom = state.AddedOnFrom;
            AddedOnTo = state.AddedOnTo;
            TypePublication = state.TypePublication;
            TypePresentation = state.TypePresentation;
            TypeWhitePaper = state.TypeWhitePaper;
            TypeSlideDeck = state.TypeSlideDeck;
            TypeReport = state.TypeReport;
            TypeOther = state.TypeOther;
            NotifyFiltersChanged();
        }

        private void NotifyFiltersChanged()
        {
            OnPropertyChanged(nameof(UseFullTextSearch));
            OnPropertyChanged(nameof(FullTextQuery));
            OnPropertyChanged(nameof(FullTextInTitle));
            OnPropertyChanged(nameof(FullTextInAbstract));
            OnPropertyChanged(nameof(FullTextInContent));
            OnPropertyChanged(nameof(IsMetadataSearch));
            OnPropertyChanged(nameof(TitleContains));
            OnPropertyChanged(nameof(AuthorContains));
            OnPropertyChanged(nameof(TagsCsv));
            OnPropertyChanged(nameof(IsInternal));
            OnPropertyChanged(nameof(YearFrom));
            OnPropertyChanged(nameof(YearTo));
            OnPropertyChanged(nameof(SourceContains));
            OnPropertyChanged(nameof(InternalIdContains));
            OnPropertyChanged(nameof(DoiContains));
            OnPropertyChanged(nameof(PmidContains));
            OnPropertyChanged(nameof(NctContains));
            OnPropertyChanged(nameof(AddedByContains));
            OnPropertyChanged(nameof(AddedOnFrom));
            OnPropertyChanged(nameof(AddedOnTo));
            OnPropertyChanged(nameof(TypePublication));
            OnPropertyChanged(nameof(TypePresentation));
            OnPropertyChanged(nameof(TypeWhitePaper));
            OnPropertyChanged(nameof(TypeSlideDeck));
            OnPropertyChanged(nameof(TypeReport));
            OnPropertyChanged(nameof(TypeOther));
        }

        private string BuildDefaultPresetName(IReadOnlyList<LibraryFilterPreset> existing)
        {
            static string? Trimmed(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return null;
                var trimmed = value.Trim();
                return trimmed.Length > 40 ? trimmed[..40] : trimmed;
            }

            var candidates = new[]
            {
                Trimmed(TitleContains),
                Trimmed(AuthorContains),
                Trimmed(SourceContains),
                Trimmed(TagsCsv)
            };

            var first = candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            if (!string.IsNullOrWhiteSpace(first))
                return first!;

            var index = existing.Count + 1;
            string name;
            do
            {
                name = $"Preset {index}";
                index++;
            } while (existing.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

            return name;
        }
    }
}
