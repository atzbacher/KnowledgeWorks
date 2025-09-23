using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Core.Models.Search;

namespace LM.App.Wpf.ViewModels.Library
{
    /// <summary>
    /// Encapsulates metadata filter state, full-text options, and preset persistence for the Library view.
    /// </summary>
    public sealed partial class LibraryFiltersViewModel : ViewModelBase
    {
        private readonly LibraryFilterPresetStore _presetStore;
        private readonly ILibraryPresetPrompt _presetPrompt;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMetadataSearch))]
        private bool useFullTextSearch;

        [ObservableProperty]
        private string? fullTextQuery;

        [ObservableProperty]
        private bool fullTextInTitle = true;

        [ObservableProperty]
        private bool fullTextInAbstract = true;

        [ObservableProperty]
        private bool fullTextInContent = true;

        [ObservableProperty]
        private string? titleContains;

        [ObservableProperty]
        private string? authorContains;

        [ObservableProperty]
        private string? tagsCsv;

        [ObservableProperty]
        private bool? isInternal;

        [ObservableProperty]
        private int? yearFrom;

        [ObservableProperty]
        private int? yearTo;

        [ObservableProperty]
        private string? sourceContains;

        [ObservableProperty]
        private string? internalIdContains;

        [ObservableProperty]
        private string? doiContains;

        [ObservableProperty]
        private string? pmidContains;

        [ObservableProperty]
        private string? nctContains;

        [ObservableProperty]
        private string? addedByContains;

        [ObservableProperty]
        private DateTime? addedOnFrom;

        [ObservableProperty]
        private DateTime? addedOnTo;

        [ObservableProperty]
        private bool typePublication = true;

        [ObservableProperty]
        private bool typePresentation = true;

        [ObservableProperty]
        private bool typeWhitePaper = true;

        [ObservableProperty]
        private bool typeSlideDeck = true;

        [ObservableProperty]
        private bool typeReport = true;

        [ObservableProperty]
        private bool typeOther = true;

        public LibraryFiltersViewModel(LibraryFilterPresetStore presetStore, ILibraryPresetPrompt presetPrompt)
        {
            _presetStore = presetStore ?? throw new ArgumentNullException(nameof(presetStore));
            _presetPrompt = presetPrompt ?? throw new ArgumentNullException(nameof(presetPrompt));

        }

        public bool IsMetadataSearch => !UseFullTextSearch;

        [RelayCommand]
        public void Clear()
        {
            UseFullTextSearch = false;
            FullTextQuery = null;
            FullTextInTitle = true;
            FullTextInAbstract = true;
            FullTextInContent = true;
            TitleContains = null;
            AuthorContains = null;
            TagsCsv = null;
            IsInternal = null;
            YearFrom = null;
            YearTo = null;
            SourceContains = null;
            InternalIdContains = null;
            DoiContains = null;
            PmidContains = null;
            NctContains = null;
            AddedByContains = null;
            AddedOnFrom = null;
            AddedOnTo = null;
            TypePublication = true;
            TypePresentation = true;
            TypeWhitePaper = true;
            TypeSlideDeck = true;
            TypeReport = true;
            TypeOther = true;
        }

        public EntryFilter BuildEntryFilter()
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

            return new EntryFilter
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
        }

        public string? GetNormalizedFullTextQuery()
        {
            if (string.IsNullOrWhiteSpace(FullTextQuery))
                return null;
            var trimmed = FullTextQuery.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        public FullTextSearchQuery BuildFullTextQuery(string queryText)
            => new()
            {
                Text = queryText,
                Fields = BuildFullTextFieldMask(),
                YearFrom = YearFrom,
                YearTo = YearTo,
                IsInternal = IsInternal,
                TypesAny = GetSelectedTypesOrNull()
            };

        public LibraryFilterState CaptureState()
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

        public void ApplyState(LibraryFilterState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));

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
        }

        private EntryType[]? GetSelectedTypesOrNull()
        {
            var list = new List<EntryType>();
            if (TypePublication) list.Add(EntryType.Publication);
            if (TypePresentation) list.Add(EntryType.Presentation);
            if (TypeWhitePaper) list.Add(EntryType.WhitePaper);
            if (TypeSlideDeck) list.Add(EntryType.SlideDeck);
            if (TypeReport) list.Add(EntryType.Report);
            if (TypeOther) list.Add(EntryType.Other);
            return list.Count > 0 ? list.ToArray() : null;
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

                await _presetStore.SavePresetAsync(preset).ConfigureAwait(false);
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

        [RelayCommand]
        private async Task LoadPresetAsync()
        {
            try
            {
                var presets = await _presetStore.ListPresetsAsync().ConfigureAwait(false);
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
                    new LibraryPresetSelectionContext(summaries, AllowLoad: true, "Load Library Preset"))
                    .ConfigureAwait(false);

                if (result is null)
                    return;

                await DeletePresetsAsync(result.DeletedPresetNames).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(result.SelectedPresetName))
                    return;

                var preset = await _presetStore.TryGetPresetAsync(result.SelectedPresetName!).ConfigureAwait(false);
                if (preset is null)
                {
                    System.Windows.MessageBox.Show(
                        $"Preset \"{result.SelectedPresetName}\" could not be found.",
                        "Load Library Preset",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                ApplyState(preset.State);
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

        [RelayCommand]
        private async Task ManagePresetsAsync()
        {
            try
            {
                var presets = await _presetStore.ListPresetsAsync().ConfigureAwait(false);
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
                    new LibraryPresetSelectionContext(summaries, AllowLoad: false, "Manage Library Presets"))
                    .ConfigureAwait(false);

                if (result is null)
                    return;

                await DeletePresetsAsync(result.DeletedPresetNames).ConfigureAwait(false);
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

                await _presetStore.DeletePresetAsync(name).ConfigureAwait(false);
            }
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
