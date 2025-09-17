#nullable enable
using LM.App.Wpf.Common;             // ViewModelBase, AsyncRelayCommand, RelayCommand
using LM.Core.Abstractions;
using LM.HubSpoke.Models;
using LM.Core.Models;            // LitSearchHook, LitSearchRun, JsonStd
using LM.Infrastructure.Pubmed;
using LM.Infrastructure.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LM.App.Wpf.ViewModels
{
    public sealed class SearchViewModel : ViewModelBase
    {
        private readonly PubMedSearchProvider _pubmed = new();
        private readonly ClinicalTrialsGovSearchProvider _ctgov = new();
        private readonly IEntryStore _store;
        private readonly IFileStorageRepository _storage;
        private readonly IWorkSpaceService _ws;

        public SearchViewModel(IEntryStore store, IFileStorageRepository storage, IWorkSpaceService ws)
        {
            _store = store;
            _storage = storage;
            _ws = ws;

            RunSearchCommand = new AsyncRelayCommand(RunSearchAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Query));
            SaveSearchCommand = new AsyncRelayCommand(SaveSearchAsync, () => !IsBusy && Results.Any());
            LoadSearchCommand = new AsyncRelayCommand(LoadSearchAsync, () => !IsBusy);
            ExportSearchCommand = new AsyncRelayCommand(ExportSearchAsync, () => !IsBusy && Results.Any());
        }

        private string _query = string.Empty;
        public string Query
        {
            get => _query;
            set
            {
                if (_query == value) return;
                _query = value;
                OnPropertyChanged();
                RaiseCanExec();
            }
        }

        public IReadOnlyList<SearchDatabaseOption> Databases { get; } = new[]
        {
            new SearchDatabaseOption(SearchDatabase.PubMed, "PubMed"),
            new SearchDatabaseOption(SearchDatabase.ClinicalTrialsGov, "ClinicalTrials.gov"),
        };

        private SearchDatabase _selectedDatabase = SearchDatabase.PubMed;
        public SearchDatabase SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (_selectedDatabase == value) return;
                _selectedDatabase = value;
                OnPropertyChanged();
            }
        }
        private DateTime? _from;
        public DateTime? From
        {
            get => _from;
            set
            {
                if (_from == value) return;
                _from = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _to;
        public DateTime? To
        {
            get => _to;
            set
            {
                if (_to == value) return;
                _to = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SearchHit> Results { get; } = new();
        public ObservableCollection<LitSearchRun> PreviousRuns { get; } = new();

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); RaiseCanExec(); } }

        public ICommand RunSearchCommand { get; }
        public ICommand SaveSearchCommand { get; }
        public ICommand LoadSearchCommand { get; }
        public ICommand ExportSearchCommand { get; }

        private void RaiseCanExec()
        {
            (RunSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (SaveSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (LoadSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (ExportSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
        }

        private async Task RunSearchAsync()
        {
            IsBusy = true;
            Results.Clear();

            try
            {
                var ct = CancellationToken.None;
                var hits = SelectedDatabase switch
                {
                    SearchDatabase.PubMed => await _pubmed.SearchAsync(Query, From, To, ct),
                    SearchDatabase.ClinicalTrialsGov => await _ctgov.SearchAsync(Query, From, To, ct),
                    _ => Array.Empty<SearchHit>()
                };

                // Mark what's already in DB (by DOI/PMID/NCT, else near-match title+year)
                var all = new List<Entry>();
                await foreach (var entry in _store.EnumerateAsync(ct))
                {
                    all.Add(entry);
                }
                // small sets expected
                foreach (var h in hits)
                {
                    bool inDb = all.Any(e =>
                           (!string.IsNullOrWhiteSpace(h.Doi) && string.Equals(e.Doi, h.Doi, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(h.ExternalId) && (
                               string.Equals(e.Pmid, h.ExternalId, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(e.Nct, h.ExternalId, StringComparison.OrdinalIgnoreCase)))
                        || (string.Equals(e.Title, h.Title, StringComparison.OrdinalIgnoreCase) && e.Year == h.Year));
                    h.AlreadyInDb = inDb;
                    Results.Add(h);
                }

                // Update "previous runs" view with in-memory run
                PreviousRuns.Insert(0, new LitSearchRun
                {
                    Provider = SelectedDatabase == SearchDatabase.PubMed ? "pubmed" : "ctgov",
                    Query = Query,
                    From = From,
                    To = To,
                    TotalHits = Results.Count
                });
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Save a LitSearch entry + run snapshot, and commit selected hits not yet in DB.
        /// </summary>
        private async Task SaveSearchAsync()
        {
            IsBusy = true;
            try
            {
                // 1) Create litsearch hook payload (definition + this run)
                var hook = new LitSearchHook
                {
                    Title = Query.Length > 80 ? Query[..80] + "…" : Query,
                    Query = Query,
                    Provider = SelectedDatabase == SearchDatabase.PubMed ? "pubmed" : "ctgov",
                    From = From,
                    To = To,
                    CreatedBy = Environment.UserName,
                    CreatedUtc = DateTime.UtcNow
                };
                var run = new LitSearchRun
                {
                    Provider = hook.Provider,
                    Query = hook.Query,
                    From = hook.From,
                    To = hook.To,
                    RunUtc = DateTime.UtcNow,
                    TotalHits = Results.Count
                };

                // Save raw provider return (debug): serialize normalized hits as JSON
                var tmpRaw = Path.Combine(Path.GetTempPath(), $"search_raw_{run.RunId}.json");
                await File.WriteAllTextAsync(tmpRaw, JsonSerializer.Serialize(Results, LM.Core.Utils.JsonEx.Options));
                var rawRel = await _storage.SaveNewAsync(tmpRaw, "litsearch/raw", preferredFileName: $"raw_{run.RunId}.json");
                run.RawAttachments.Add(rawRel);

                hook.Runs.Add(run);

                // 2) Persist litsearch.json as the "primary file" of the LitSearch entry
                var tmpHook = Path.Combine(Path.GetTempPath(), $"litsearch_{run.RunId}.json");
                await File.WriteAllTextAsync(tmpHook, JsonSerializer.Serialize(hook, JsonStd.Options), Encoding.UTF8);
                var relHook = await _storage.SaveNewAsync(tmpHook, "litsearch", preferredFileName: $"litsearch_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

                var litEntry = new Entry
                {
                    Id = LM.Core.Utils.IdGen.NewId(),
                    Type = EntryType.Other,
                    Title = hook.Title,
                    DisplayName = hook.Title,
                    Source = "LitSearch",
                    AddedOnUtc = DateTime.UtcNow,
                    MainFilePath = relHook,                 // primary (will be used by LitSearchSpokeHandler)
                    OriginalFileName = Path.GetFileName(relHook),
                    Links = new() { },
                    Tags = new(),
                    Authors = new()
                };

                await _store.SaveAsync(litEntry);          // hub+spoke save (our spoke reads the primary JSON) :contentReference[oaicite:5]{index=5}

                // 3) Commit selected hits not in DB as unique entries (no files)
                foreach (var h in Results.Where(r => r.Selected && !r.AlreadyInDb))
                {
                    var url = h.Url ?? (h.Doi != null ? $"https://doi.org/{h.Doi}" : null);
                    // create a tiny .url file so MainFilePath is valid
                    var tmpUrl = Path.Combine(Path.GetTempPath(), $"{Sanitize(h.Title)}.url");
                    await File.WriteAllTextAsync(tmpUrl, $"[InternetShortcut]{Environment.NewLine}URL={(url ?? "about:blank")}");

                    var rel = await _storage.SaveNewAsync(tmpUrl, "external");
                    var entry = new Entry
                    {
                        Id = LM.Core.Utils.IdGen.NewId(),
                        Type = h.Source == SearchDatabase.PubMed ? EntryType.Publication : EntryType.Other,
                        Title = h.Title,
                        DisplayName = h.Title,
                        Year = h.Year,
                        Doi = h.Doi,
                        Pmid = h.Source == SearchDatabase.PubMed ? h.ExternalId : null,
                        Nct = h.Source == SearchDatabase.ClinicalTrialsGov ? h.ExternalId : null,
                        Authors = LM.Infrastructure.Utils.TagNormalizer.SplitAndNormalize(h.Authors).ToList(),
                        Links = url is null ? new() : new() { url },
                        Source = h.Source == SearchDatabase.PubMed ? "PubMed" : "ClinicalTrials.gov",
                        AddedOnUtc = DateTime.UtcNow,
                        MainFilePath = rel,
                        OriginalFileName = Path.GetFileName(rel)
                    };
                    await _store.SaveAsync(entry);          // store it (Article/Document spokes handle indexing) :contentReference[oaicite:6]{index=6}

                    run.ImportedEntryIds.Add(entry.Id);
                }

                // 4) Update (append) run info in the litsearch hook and re-save the entry
                // Re-write the hook (now with ImportedEntryIds filled)
                await File.WriteAllTextAsync(tmpHook, JsonSerializer.Serialize(hook, JsonStd.Options), Encoding.UTF8);
                relHook = await _storage.SaveNewAsync(tmpHook, "litsearch", preferredFileName: $"litsearch_{DateTime.UtcNow:yyyyMMdd_HHmmss}_updated.json");
                litEntry.MainFilePath = relHook;
                litEntry.OriginalFileName = Path.GetFileName(relHook);
                await _store.SaveAsync(litEntry);

                // Show in "Previous runs"
                PreviousRuns.Insert(0, run);
            }
            finally { IsBusy = false; }
        }

        private async Task LoadSearchAsync()
        {
            // Minimal: load the last LitSearch entry to pre-fill From/To/Query.
            var all = new List<Entry>();
            await foreach (var entry in _store.EnumerateAsync(CancellationToken.None))
            {
                all.Add(entry);
            }
            var last = all.Where(e => string.Equals(e.Source, "LitSearch", StringComparison.OrdinalIgnoreCase))
                          .OrderByDescending(e => e.AddedOnUtc).FirstOrDefault();
            if (last?.MainFilePath is null) return;

            var abs = _ws.GetAbsolutePath(last.MainFilePath);
            var json = await File.ReadAllTextAsync(abs);
            var hook = JsonSerializer.Deserialize<LitSearchHook>(json, JsonStd.Options);
            if (hook is null) return;

            Query = hook.Query;
            // "When loading old searches, the end date of a previous search will be the start date of the new"
            From = hook.To;
            To = null;
            SelectedDatabase = hook.Provider.Equals("pubmed", StringComparison.OrdinalIgnoreCase) ? SearchDatabase.PubMed : SearchDatabase.ClinicalTrialsGov;
        }

        private async Task ExportSearchAsync()
        {
            // Exports current grid to CSV beside the workspace local DB (simple path choice)
            var dir = Path.GetDirectoryName(_ws.GetLocalDbPath()) ?? _ws.GetWorkspaceRoot();
            var path = Path.Combine(dir, $"search_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("Source,ExternalId,DOI,Year,Authors,Title,Journal/Source,URL,InDB,Selected");
            foreach (var r in Results)
            {
                string esc(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
                sb.AppendLine(string.Join(",",
                    esc(r.Source.ToString()), esc(r.ExternalId), esc(r.Doi), esc(r.Year?.ToString()),
                    esc(r.Authors), esc(r.Title), esc(r.JournalOrSource), esc(r.Url),
                    esc(r.AlreadyInDb.ToString()), esc(r.Selected.ToString())
                ));
            }
            await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            return name.Length > 120 ? name[..120] : name;
        }
    }

    public sealed record SearchDatabaseOption
    {
        public SearchDatabaseOption(SearchDatabase value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public SearchDatabase Value { get; }

        public string DisplayName { get; }
    }
}
