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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
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
        private readonly ISearchSavePrompt _savePrompt;
        private readonly Dictionary<string, PreviousSearchContext> _runIndex = new(StringComparer.Ordinal);

        private string? _loadedEntryId;
        private LitSearchHook? _loadedHook;

        public SearchViewModel(IEntryStore store, IFileStorageRepository storage, IWorkSpaceService ws, ISearchSavePrompt savePrompt)
        {
            _store = store;
            _storage = storage;
            _ws = ws;
            _savePrompt = savePrompt ?? throw new ArgumentNullException(nameof(savePrompt));

            RunSearchCommand = new AsyncRelayCommand(RunSearchAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Query));
            SaveSearchCommand = new AsyncRelayCommand(SaveSearchAsync, () => !IsBusy && Results.Any());
            LoadSearchCommand = new AsyncRelayCommand(LoadSearchAsync, () => !IsBusy && SelectedPreviousRun is not null);
            ExportSearchCommand = new AsyncRelayCommand(ExportSearchAsync, () => !IsBusy && Results.Any());
            StartPreviousRunCommand = new AsyncRelayCommand(StartPreviousRunAsync, p => !IsBusy && p is LitSearchRun);
            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, p => !IsBusy && p is LitSearchRun);
            ShowRunDetailsCommand = new AsyncRelayCommand(ShowRunDetailsAsync, p => !IsBusy && p is LitSearchRun);

            _ = RefreshPreviousRunsAsync();

            PreviousRuns.CollectionChanged += OnPreviousRunsCollectionChanged;
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

        public int PreviousRunsCount => PreviousRuns.Count;

        private LitSearchRun? _selectedPreviousRun;
        public LitSearchRun? SelectedPreviousRun
        {
            get => _selectedPreviousRun;
            set
            {
                if (ReferenceEquals(_selectedPreviousRun, value)) return;
                _selectedPreviousRun = value;
                OnPropertyChanged();
                RaiseCanExec();
            }
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); RaiseCanExec(); } }

        public ICommand RunSearchCommand { get; }
        public ICommand SaveSearchCommand { get; }
        public ICommand LoadSearchCommand { get; }
        public ICommand ExportSearchCommand { get; }
        public ICommand StartPreviousRunCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand ShowRunDetailsCommand { get; }

        private void RaiseCanExec()
        {
            (RunSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (SaveSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (LoadSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (ExportSearchCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (StartPreviousRunCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (ToggleFavoriteCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
            (ShowRunDetailsCommand as AsyncRelayCommand)!.RaiseCanExecuteChanged();
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

            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Save a LitSearch entry + run snapshot, and commit selected hits not yet in DB.
        /// </summary>
        private async Task SaveSearchAsync()
        {
            var continueExisting = _loadedHook is not null && _loadedEntryId is not null
                && string.Equals(_loadedHook.Query, Query, StringComparison.Ordinal);

            var fallbackTitle = BuildTitle(Query, _loadedHook, continueExisting);
            var defaultName = _loadedHook?.Title;
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = SelectedPreviousRun?.DisplayName;
            }
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = fallbackTitle;
            }

            var defaultNotes = _loadedHook?.Notes ?? string.Empty;

            var promptResult = await _savePrompt.RequestAsync(new SearchSavePromptContext(
                Query,
                SelectedDatabase,
                From,
                To,
                defaultName ?? string.Empty,
                defaultNotes ?? string.Empty));

            if (promptResult is null)
            {
                return;
            }

            var trimmedName = string.IsNullOrWhiteSpace(promptResult.Name)
                ? null
                : promptResult.Name.Trim();
            var trimmedNotes = string.IsNullOrWhiteSpace(promptResult.Notes)
                ? null
                : promptResult.Notes.Trim();

            var effectiveTitle = string.IsNullOrWhiteSpace(trimmedName) ? fallbackTitle : trimmedName;

            IsBusy = true;
            try
            {
                var now = DateTime.UtcNow;
                var provider = SelectedDatabase == SearchDatabase.PubMed ? "pubmed" : "ctgov";
                var user = Environment.UserName;

                var run = new LitSearchRun
                {
                    Provider = provider,
                    Query = Query,
                    From = From,
                    To = To,
                    RunUtc = now,
                    TotalHits = Results.Count,
                    ExecutedBy = user,
                    DisplayName = effectiveTitle
                };

                // Save raw provider return (debug): serialize normalized hits as JSON
                var tmpRaw = Path.Combine(Path.GetTempPath(), $"search_raw_{run.RunId}.json");
                await File.WriteAllTextAsync(tmpRaw, JsonSerializer.Serialize(Results, LM.Core.Utils.JsonEx.Options));
                var rawRel = await _storage.SaveNewAsync(tmpRaw, "litsearch/raw", preferredFileName: $"raw_{run.RunId}.json");
                run.RawAttachments.Add(rawRel);

                var createdUtc = continueExisting ? _loadedHook!.CreatedUtc : now;
                var createdBy = continueExisting ? _loadedHook!.CreatedBy : user;
                var derivedFrom = continueExisting ? _loadedHook!.DerivedFromEntryId : _loadedEntryId;

                var runs = continueExisting ? _loadedHook!.Runs.ToList() : new List<LitSearchRun>();
                runs.Add(run);

                var note = ComposeSearchNote(Query, provider, createdUtc, createdBy, runs.Count, run, derivedFrom, trimmedNotes);

                var hook = new LitSearchHook
                {
                    Title = effectiveTitle,
                    Query = Query,
                    Provider = provider,
                    From = From,
                    To = To,
                    CreatedBy = createdBy,
                    CreatedUtc = createdUtc,
                    Keywords = _loadedHook?.Keywords ?? Array.Empty<string>(),
                    Notes = trimmedNotes,
                    DerivedFromEntryId = derivedFrom,
                    Runs = runs
                };

                // Persist litsearch.json as the "primary file" of the LitSearch entry
                var tmpHook = Path.Combine(Path.GetTempPath(), $"litsearch_{run.RunId}.json");
                await File.WriteAllTextAsync(tmpHook, JsonSerializer.Serialize(hook, JsonStd.Options), Encoding.UTF8);
                var relHook = await _storage.SaveNewAsync(tmpHook, "litsearch", preferredFileName: $"litsearch_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

                var litEntry = continueExisting && _loadedEntryId is not null
                    ? await _store.GetByIdAsync(_loadedEntryId) ?? new Entry { Id = _loadedEntryId }
                    : new Entry { Id = LM.Core.Utils.IdGen.NewId() };

                litEntry.Type = EntryType.Other;
                litEntry.Title = effectiveTitle;
                litEntry.DisplayName = effectiveTitle;
                litEntry.Source = "LitSearch";
                litEntry.AddedOnUtc = createdUtc;
                litEntry.AddedBy = createdBy;
                litEntry.Notes = note;
                litEntry.MainFilePath = relHook;                 // primary (will be used by LitSearchSpokeHandler)
                litEntry.OriginalFileName = Path.GetFileName(relHook);
                litEntry.Links ??= new List<string>();
                litEntry.Tags ??= new List<string>();
                litEntry.Authors ??= new List<string>();
                litEntry.Relations ??= new List<Relation>();

                if (!continueExisting && derivedFrom is not null)
                    EnsureRelation(litEntry, derivedFrom);
                else if (continueExisting && hook.DerivedFromEntryId is not null)
                    EnsureRelation(litEntry, hook.DerivedFromEntryId);

                await _store.SaveAsync(litEntry);          // hub+spoke save (our spoke reads the primary JSON)

                // Commit selected hits not in DB as unique entries (no files)
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
                        Links = url is null ? new List<string>() : new List<string> { url },
                        Source = h.Source == SearchDatabase.PubMed ? "PubMed" : "ClinicalTrials.gov",
                        AddedOnUtc = DateTime.UtcNow,
                        MainFilePath = rel,
                        OriginalFileName = Path.GetFileName(rel)
                    };
                    await _store.SaveAsync(entry);          // store it (Article/Document spokes handle indexing)

                    run.ImportedEntryIds.Add(entry.Id);
                    h.AlreadyInDb = true;
                }

                // Re-write the hook (now with ImportedEntryIds filled)
                await File.WriteAllTextAsync(tmpHook, JsonSerializer.Serialize(hook, JsonStd.Options), Encoding.UTF8);
                relHook = await _storage.SaveNewAsync(tmpHook, "litsearch", preferredFileName: $"litsearch_{DateTime.UtcNow:yyyyMMdd_HHmmss}_updated.json");
                litEntry.MainFilePath = relHook;
                litEntry.OriginalFileName = Path.GetFileName(relHook);
                await _store.SaveAsync(litEntry);

                _loadedEntryId = litEntry.Id;
                _loadedHook = hook;

                await RefreshPreviousRunsAsync();
                SelectedPreviousRun = PreviousRuns.FirstOrDefault(r => r.RunId == run.RunId) ?? SelectedPreviousRun;
            }
            finally { IsBusy = false; }
        }

        private async Task LoadSearchAsync()
        {
            await RefreshPreviousRunsAsync();

            var run = SelectedPreviousRun ?? PreviousRuns.FirstOrDefault();
            if (run is null)
                return;

            _ = TryLoadRun(run);
        }

        private async Task StartPreviousRunAsync(object? parameter)
        {
            if (parameter is not LitSearchRun requestedRun)
                return;

            await RefreshPreviousRunsAsync();

            var run = PreviousRuns.FirstOrDefault(r => string.Equals(r.RunId, requestedRun.RunId, StringComparison.Ordinal))
                      ?? requestedRun;

            if (!TryLoadRun(run))
                return;

            await RunSearchAsync();
        }

        private async Task ToggleFavoriteAsync(object? parameter)
        {
            if (parameter is not LitSearchRun run)
                return;

            if (!_runIndex.TryGetValue(run.RunId, out var context))
                return;

            IsBusy = true;
            try
            {
                var runs = context.Hook.Runs;
                var index = runs.FindIndex(r => string.Equals(r.RunId, run.RunId, StringComparison.Ordinal));
                if (index < 0)
                    return;

                var updated = CloneRunWithMetadata(run, run.RunId, run.DisplayName, !run.IsFavorite);
                runs[index] = updated;

                await PersistHookAsync(context);
                await RefreshPreviousRunsAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnPreviousRunsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => OnPropertyChanged(nameof(PreviousRunsCount));

        private async Task ShowRunDetailsAsync(object? parameter)
        {
            if (parameter is not LitSearchRun run)
                return;

            if (!_runIndex.TryGetValue(run.RunId, out var context))
                return;

            try
            {
                var details = new
                {
                    entryId = context.EntryId,
                    hook = new
                    {
                        context.Hook.Title,
                        context.Hook.Query,
                        context.Hook.Provider,
                        context.Hook.From,
                        context.Hook.To,
                        context.Hook.CreatedBy,
                        context.Hook.CreatedUtc,
                        context.Hook.DerivedFromEntryId,
                        context.Hook.Keywords,
                        context.Hook.Notes,
                        runCount = context.Hook.Runs.Count
                    },
                    run = new
                    {
                        run.RunId,
                        run.Provider,
                        run.Query,
                        run.From,
                        run.To,
                        run.RunUtc,
                        run.TotalHits,
                        run.ExecutedBy,
                        run.DisplayName,
                        run.IsFavorite,
                        run.RawAttachments,
                        run.ImportedEntryIds
                    }
                };

                var json = JsonSerializer.Serialize(details, JsonStd.Options);
                var tmpPath = Path.Combine(Path.GetTempPath(), $"litsearch_run_{run.RunId}.json");
                await File.WriteAllTextAsync(tmpPath, json, Encoding.UTF8);

                var startInfo = new ProcessStartInfo
                {
                    FileName = tmpPath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch
            {
                // ignore errors when trying to surface diagnostics; best-effort feature
            }
        }

        private bool TryLoadRun(LitSearchRun run)
        {
            if (!_runIndex.TryGetValue(run.RunId, out var context))
                return false;

            SelectedPreviousRun = run;

            Query = run.Query;
            From = run.To ?? context.Hook.To ?? context.Hook.From ?? run.From;
            To = null;
            SelectedDatabase = run.Provider.Equals("pubmed", StringComparison.OrdinalIgnoreCase)
                ? SearchDatabase.PubMed
                : SearchDatabase.ClinicalTrialsGov;

            _loadedEntryId = context.EntryId;
            _loadedHook = context.Hook;

            Results.Clear();
            return true;
        }

        private async Task RefreshPreviousRunsAsync(CancellationToken ct = default)
        {
            var items = new List<(LitSearchRun run, PreviousSearchContext context)>();
            _runIndex.Clear();

            try
            {
                var root = _ws.GetWorkspaceRoot();
                await foreach (var entry in _store.EnumerateAsync(ct))
                {
                    if (ct.IsCancellationRequested)
                        break;

                    if (!string.Equals(entry.Source, "LitSearch", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.IsNullOrWhiteSpace(entry.Id))
                        continue;

                    var hookPath = Path.Combine(root, "entries", entry.Id, "litsearch", "litsearch.json");
                    if (!File.Exists(hookPath))
                        continue;

                    try
                    {
                        var json = await File.ReadAllTextAsync(hookPath, ct);
                        var hook = JsonSerializer.Deserialize<LitSearchHook>(json, JsonStd.Options);
                        if (hook is null)
                            continue;

                        var context = new PreviousSearchContext(entry.Id, hookPath, hook);
                        var runs = hook.Runs;
                        var hookUpdated = false;

                        for (var i = 0; i < runs.Count; i++)
                        {
                            var run = runs[i];
                            if (string.IsNullOrWhiteSpace(run.RunId))
                            {
                                var generatedId = LM.Core.Utils.IdGen.NewId();
                                run = CloneRunWithMetadata(run, generatedId, run.DisplayName, run.IsFavorite);
                                runs[i] = run;
                                hookUpdated = true;
                            }

                            items.Add((run, context));
                            _runIndex[run.RunId] = context;
                        }

                        if (hookUpdated)
                        {
                            await PersistHookAsync(context, ct);
                        }
                    }
                    catch
                    {
                        // ignore malformed entries
                    }
                }
            }
            catch
            {
                // workspace not yet initialized
            }

            items.Sort((a, b) =>
            {
                var favoriteCompare = b.run.IsFavorite.CompareTo(a.run.IsFavorite);
                if (favoriteCompare != 0)
                    return favoriteCompare;
                return DateTime.Compare(b.run.RunUtc, a.run.RunUtc);
            });

            var previouslySelected = SelectedPreviousRun;
            var previouslySelectedId = previouslySelected?.RunId;

            PreviousRuns.Clear();
            foreach (var (run, _) in items)
                PreviousRuns.Add(run);

            if (PreviousRuns.Count == 0)
            {
                SelectedPreviousRun = null;
            }
            else
            {
                var match = previouslySelectedId is null
                    ? null
                    : PreviousRuns.FirstOrDefault(r => string.Equals(r.RunId, previouslySelectedId, StringComparison.Ordinal));

                if (match is null && previouslySelected is not null)
                {
                    match = PreviousRuns.FirstOrDefault(r =>
                        r.RunUtc == previouslySelected.RunUtc &&
                        string.Equals(r.Provider, previouslySelected.Provider, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Query, previouslySelected.Query, StringComparison.Ordinal));
                }

                if (match is not null)
                {
                    SelectedPreviousRun = match;
                }
                else if (SelectedPreviousRun is null || !_runIndex.ContainsKey(SelectedPreviousRun.RunId))
                {
                    SelectedPreviousRun = PreviousRuns[0];
                }
            }

            RaiseCanExec();
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

        private static string BuildTitle(string query, LitSearchHook? existing, bool reuseExisting)
        {
            if (reuseExisting && !string.IsNullOrWhiteSpace(existing?.Title))
                return existing!.Title;

            var trimmed = (query ?? string.Empty).Trim();
            return trimmed.Length > 80 ? trimmed[..80] + "…" : trimmed;
        }

        private static string ComposeSearchNote(string query, string provider, DateTime createdUtc, string? createdBy, int runCount, LitSearchRun latestRun, string? derivedFrom, string? userNotes)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(userNotes))
            {
                sb.AppendLine(userNotes.Trim());
                sb.AppendLine();
            }
            sb.AppendLine($"Query: {query}");
            sb.AppendLine($"Provider: {provider}");
            sb.AppendLine($"Created by {createdBy ?? "unknown"} on {createdUtc:u}.");
            sb.AppendLine($"Run count: {runCount}");
            sb.AppendLine($"Latest run executed by {latestRun.ExecutedBy ?? createdBy ?? "unknown"} on {latestRun.RunUtc:u} (hits: {latestRun.TotalHits}).");
            if (latestRun.From.HasValue || latestRun.To.HasValue)
            {
                var fromText = latestRun.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "–";
                var toText = latestRun.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "–";
                sb.AppendLine($"Range: {fromText} → {toText}");
            }
            if (!string.IsNullOrWhiteSpace(derivedFrom))
            {
                sb.AppendLine($"Derived from entry {derivedFrom}.");
            }
            return sb.ToString().Trim();
        }

        private static void EnsureRelation(Entry entry, string targetEntryId)
        {
            if (string.IsNullOrWhiteSpace(targetEntryId))
                return;

            entry.Relations ??= new List<Relation>();

            if (entry.Relations.Any(r =>
                    string.Equals(r.Type, "derived_from", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.TargetEntryId, targetEntryId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            entry.Relations.Add(new Relation { Type = "derived_from", TargetEntryId = targetEntryId });
        }

        private async Task PersistHookAsync(PreviousSearchContext context, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(context.Hook, JsonStd.Options);
                await File.WriteAllTextAsync(context.HookPath, json, Encoding.UTF8, ct);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private static LitSearchRun CloneRunWithMetadata(LitSearchRun source, string runId, string? displayName, bool isFavorite)
        {
            return new LitSearchRun
            {
                RunId = runId,
                Provider = source.Provider,
                Query = source.Query,
                From = source.From,
                To = source.To,
                RunUtc = source.RunUtc,
                TotalHits = source.TotalHits,
                ExecutedBy = source.ExecutedBy,
                RawAttachments = source.RawAttachments is null ? new List<string>() : new List<string>(source.RawAttachments),
                ImportedEntryIds = source.ImportedEntryIds is null ? new List<string>() : new List<string>(source.ImportedEntryIds),
                DisplayName = displayName,
                IsFavorite = isFavorite
            };
        }

        private sealed record PreviousSearchContext(string EntryId, string HookPath, LitSearchHook Hook);

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
