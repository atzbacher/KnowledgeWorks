using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LM.App.Wpf.Common;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>
    /// Fielded metadata search against the JSON-backed entry store for the current workspace.
    /// </summary>
    public sealed class LibraryViewModel : ViewModelBase
    {
        private readonly IEntryStore _store;
        private readonly IWorkSpaceService _ws;
        private Entry? _selected;
        private string? _sourceContains;
        private string? _internalIdContains;
        private string? _doiContains;
        private string? _pmidContains;
        private string? _nctContains;
        private string? _addedByContains;
        private DateTime? _addedOnFrom;
        private DateTime? _addedOnTo;

        public LibraryViewModel(IEntryStore store, IWorkSpaceService ws)
        {
            _store = store;
            _ws = ws;

            SearchCommand = new AsyncRelayCommand(SearchAsync);
            ClearCommand  = new RelayCommand(_ => ClearFilters());
            OpenCommand   = new RelayCommand(_ => OpenSelected(), _ => Selected is not null);

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

        public ObservableCollection<Entry> Results { get; } = new();

        public Entry? Selected
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(); (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand OpenCommand { get; }

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
                    TypesAny = TypePublication || TypePresentation || TypeReport || TypeWhitePaper || TypeSlideDeck || TypeOther
                        ? BuildTypeFilter()
                        : null,   // only filter if user selected something
                    YearFrom = YearFrom,
                    YearTo = YearTo,
                    IsInternal = IsInternal.HasValue ? IsInternal : null, // only filter if user touched it
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
    $"YearFrom={filter.YearFrom}, YearTo={filter.YearTo}, IsInternal={filter.IsInternal}"
);
                var rows = await _store.SearchAsync(filter);

                // DEBUG: how many did we get?
                Debug.WriteLine($"[LibraryViewModel] SearchAsync → {rows.Count} rows");

                foreach (var r in rows)
                {
                    // Defensive: ensure Title/DisplayName are never null
                    r.Title ??= "(untitled)";
                    r.DisplayName ??= r.Title;

                    // Optional: join collections for display if XAML is binding directly
                    if (r.Authors != null && r.Authors.Count > 0)
                        r.DisplayName += $" — {string.Join(", ", r.Authors)}";

                    Results.Add(r);
                }

                // DEBUG: reflect back into UI even if 0
                if (rows.Count == 0)
                    Debug.WriteLine("[LibraryViewModel] No entries matched filter");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryViewModel] SearchAsync FAILED: {ex}");
                throw; // or swallow if you prefer
            }
        }




        private void ClearFilters()
        {
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

        private void OpenSelected()
        {
            if (Selected is null) return;
            try
            {
                var abs = _ws.GetAbsolutePath(Selected.MainFilePath);
                Process.Start(new ProcessStartInfo { FileName = abs, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open file:\n{ex.Message}", "Open Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
