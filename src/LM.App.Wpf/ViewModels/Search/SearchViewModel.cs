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
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
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
        private readonly Dictionary<string, PreviousSearchContext> _entryIndex = new(StringComparer.Ordinal);

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
            StartPreviousRunCommand = new AsyncRelayCommand(StartPreviousRunAsync, p => !IsBusy && p is PreviousSearchSummary);
            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, p => !IsBusy && p is PreviousSearchSummary);
            ShowRunDetailsCommand = new AsyncRelayCommand(ShowRunDetailsAsync, p => !IsBusy && p is PreviousSearchSummary);

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
        public ObservableCollection<PreviousSearchSummary> PreviousRuns { get; } = new();

        public int PreviousRunsCount => PreviousRuns.Count;

        private PreviousSearchSummary? _selectedPreviousRun;
        public PreviousSearchSummary? SelectedPreviousRun
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
                    var match = FindExistingEntry(all, h);
                    h.AlreadyInDb = match is not null;
                    h.ExistingEntryId = match?.Id;
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
            var continueExisting = ShouldContinueExisting(_loadedHook, _loadedEntryId, Query);

            var fallbackTitle = BuildTitle(Query, _loadedHook, continueExisting);
            string defaultName;
            if (continueExisting)
            {
                defaultName = _loadedHook?.Title ?? SelectedPreviousRun?.DisplayName ?? fallbackTitle;
            }
            else if (_loadedHook is not null)
            {
                defaultName = string.Empty;
            }
            else
            {
                defaultName = fallbackTitle;
            }

            var defaultNotes = _loadedHook?.UserNotes ?? string.Empty;

            Entry? existingEntry = null;
            var defaultTags = new List<string>();
            if (continueExisting && _loadedEntryId is not null)
            {
                existingEntry = await _store.GetByIdAsync(_loadedEntryId);
                if (existingEntry?.Tags is { Count: > 0 })
                    defaultTags = new List<string>(existingEntry.Tags);
            }
            else if (SelectedPreviousRun?.Tags is { Count: > 0 })
            {
                defaultTags = new List<string>(SelectedPreviousRun.Tags);
            }

            var skipPrompt = continueExisting;

            SearchSavePromptResult? promptResult = null;
            if (!skipPrompt)
            {
                promptResult = await _savePrompt.RequestAsync(new SearchSavePromptContext(
                    Query,
                    SelectedDatabase,
                    From,
                    To,
                    defaultName ?? string.Empty,
                    defaultNotes ?? string.Empty,
                    defaultTags));

                if (promptResult is null)
                {
                    return;
                }
            }

            string? trimmedName;
            string? trimmedNotes;
            List<string> normalizedTags;

            if (skipPrompt)
            {
                trimmedName = string.IsNullOrWhiteSpace(defaultName)
                    ? null
                    : defaultName.Trim();

                trimmedNotes = string.IsNullOrWhiteSpace(defaultNotes)
                    ? null
                    : defaultNotes.Trim();

                normalizedTags = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var tag in defaultTags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                        continue;

                    var trimmedTag = tag.Trim();
                    if (trimmedTag.Length == 0)
                        continue;

                    if (seen.Add(trimmedTag))
                        normalizedTags.Add(trimmedTag);
                }
            }
            else
            {
                trimmedName = string.IsNullOrWhiteSpace(promptResult!.Name)
                    ? null
                    : promptResult.Name.Trim();
                trimmedNotes = string.IsNullOrWhiteSpace(promptResult.Notes)
                    ? null
                    : promptResult.Notes.Trim();

                normalizedTags = LM.Infrastructure.Utils.TagNormalizer
                    .SplitAndNormalize(promptResult.Tags ?? string.Empty)
                    .ToList();
            }

            var effectiveTitle = string.IsNullOrWhiteSpace(trimmedName) ? fallbackTitle : trimmedName;

            IsBusy = true;
            try
            {
                var now = DateTime.UtcNow;
                var provider = SelectedDatabase == SearchDatabase.PubMed ? "pubmed" : "ctgov";
                var user = Environment.UserName;

                var run = new LitSearchRun
                {
                    From = From,
                    To = To,
                    RunUtc = now,
                    TotalHits = Results.Count,
                    ExecutedBy = user
                };

                var createdUtc = continueExisting ? _loadedHook!.CreatedUtc : now;
                var createdBy = continueExisting ? _loadedHook!.CreatedBy : user;
                var derivedFrom = continueExisting ? _loadedHook!.DerivedFromEntryId : _loadedEntryId;

                var runs = continueExisting ? _loadedHook!.Runs.ToList() : new List<LitSearchRun>();
                runs.Add(run);

                var note = ComposeSearchNote(Query, provider, effectiveTitle, createdUtc, createdBy, runs.Count, run, derivedFrom, trimmedNotes);

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
                    UserNotes = trimmedNotes,
                    NotesSummary = string.IsNullOrWhiteSpace(trimmedNotes) ? null : note,
                    DerivedFromEntryId = derivedFrom,
                    Runs = runs
                };

                // Persist litsearch.json as the "primary file" of the LitSearch entry
                var tmpHook = Path.Combine(Path.GetTempPath(), $"litsearch_{run.RunId}.json");
                await File.WriteAllTextAsync(tmpHook, JsonSerializer.Serialize(hook, JsonStd.Options), Encoding.UTF8);
                var relHook = await _storage.SaveNewAsync(tmpHook, string.Empty, preferredFileName: $"litsearch_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

                var litEntry = continueExisting && _loadedEntryId is not null
                    ? existingEntry ?? await _store.GetByIdAsync(_loadedEntryId) ?? new Entry { Id = _loadedEntryId }
                    : new Entry { Id = LM.Core.Utils.IdGen.NewId() };

                litEntry.Type = EntryType.LitSearch;
                litEntry.Title = effectiveTitle;
                litEntry.DisplayName = effectiveTitle;
                litEntry.Source = null;
                litEntry.AddedOnUtc = createdUtc;
                litEntry.AddedBy = createdBy;
                litEntry.Notes = string.IsNullOrWhiteSpace(trimmedNotes) ? null : note;
                litEntry.UserNotes = trimmedNotes;
                litEntry.MainFilePath = relHook;                 // primary (will be used by LitSearchSpokeHandler)
                litEntry.OriginalFileName = Path.GetFileName(relHook);
                litEntry.Links ??= new List<string>();
                litEntry.Tags = normalizedTags;
                litEntry.Authors ??= new List<string>();
                litEntry.Relations ??= new List<Relation>();

                if (!continueExisting && derivedFrom is not null)
                    EnsureRelation(litEntry, derivedFrom);
                else if (continueExisting && hook.DerivedFromEntryId is not null)
                    EnsureRelation(litEntry, hook.DerivedFromEntryId);

                await _store.SaveAsync(litEntry);          // hub+spoke save (our spoke reads the primary JSON)

                var litEntryId = litEntry.Id;
                if (string.IsNullOrWhiteSpace(litEntryId))
                    throw new InvalidOperationException("LitSearch entry must have an identifier after save.");

                var runContext = EnsureLitSearchRunContext(litEntryId, run.RunId);

                // Commit selected hits not in DB as unique entries (no files)
                var selectedHits = Results.Where(r => r.Selected).ToList();
                foreach (var h in selectedHits.Where(r => !r.AlreadyInDb))
                {
                    var url = h.Url ?? (h.Doi != null ? $"https://doi.org/{h.Doi}" : null);
                    // create a tiny .url file so MainFilePath is valid
                    var tmpUrl = Path.Combine(Path.GetTempPath(), $"{Sanitize(h.Title)}.url");
                    await File.WriteAllTextAsync(tmpUrl, $"[InternetShortcut]{Environment.NewLine}URL={(url ?? "about:blank")}");

                    var rel = await _storage.SaveNewAsync(tmpUrl, string.Empty);
                    var entryId = LM.Core.Utils.IdGen.NewId();
                    var authors = SplitAuthors(h.Authors);
                    var tagList = new List<string>();
                    var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var source = NormalizeSource(h);

                    PubMedImportResult? import = null;
                    if (h.Source == SearchDatabase.PubMed && !string.IsNullOrWhiteSpace(h.ExternalId))
                    {
                        import = await TrySavePubMedXmlAsync(litEntryId, entryId, run.RunId, h.ExternalId);
                        if (import is not null)
                        {
                            if (import.Authors.Count > 0)
                                authors = import.Authors.Select(a => a.Trim())
                                                         .Where(s => !string.IsNullOrWhiteSpace(s))
                                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                                         .ToList();

                            if (import.Keywords.Count > 0)
                                foreach (var kw in import.Keywords)
                                    AddTag(tagList, tagSet, kw);

                            if (import.MeshHeadings.Count > 0)
                                foreach (var term in import.MeshHeadings)
                                    AddTag(tagList, tagSet, term);

                            if (string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(import.JournalTitle))
                                source = import.JournalTitle;
                        }
                    }

                    var entry = new Entry
                    {
                        Id = entryId,
                        Type = h.Source == SearchDatabase.PubMed ? EntryType.Publication : EntryType.Other,
                        Title = h.Title,
                        DisplayName = h.Title,
                        Year = h.Year,
                        Doi = h.Doi,
                        Pmid = h.Source == SearchDatabase.PubMed ? h.ExternalId : null,
                        Nct = h.Source == SearchDatabase.ClinicalTrialsGov ? h.ExternalId : null,
                        Authors = authors,
                        Links = url is null ? new List<string>() : new List<string> { url },
                        Source = string.IsNullOrWhiteSpace(source) ? null : source,
                        AddedOnUtc = DateTime.UtcNow,
                        AddedBy = user,
                        Tags = tagList,
                        MainFilePath = rel,
                        OriginalFileName = Path.GetFileName(rel)
                    };

                    await _store.SaveAsync(entry);          // store it (Article/Document spokes handle indexing)

                    h.AlreadyInDb = true;
                    h.ExistingEntryId = entry.Id;

                    if (import?.RelativeXmlPath is string xmlPath &&
                        !run.RawAttachments.Any(p => string.Equals(p, xmlPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        run.RawAttachments.Add(xmlPath);
                    }
                }

                var checkedEntryIds = BuildCheckedEntryIds(selectedHits);
                run.CheckedEntryIdsPath = await PersistRunCheckedEntriesAsync(runContext, run.RunId, checkedEntryIds, now);

                // Re-write the hook (with run metadata updated)
                await File.WriteAllTextAsync(tmpHook, JsonSerializer.Serialize(hook, JsonStd.Options), Encoding.UTF8);
                relHook = await _storage.SaveNewAsync(tmpHook, string.Empty, preferredFileName: $"litsearch_{DateTime.UtcNow:yyyyMMdd_HHmmss}_updated.json");
                litEntry.MainFilePath = relHook;
                litEntry.OriginalFileName = Path.GetFileName(relHook);
                await _store.SaveAsync(litEntry);

                _loadedEntryId = litEntry.Id;
                _loadedHook = hook;

                await RefreshPreviousRunsAsync();
                SelectedPreviousRun = PreviousRuns.FirstOrDefault(r => r.RunId == run.RunId) ?? SelectedPreviousRun;

                ClearSearchUiAfterSave();
            }
            finally { IsBusy = false; }
        }

        private async Task LoadSearchAsync()
        {
            await RefreshPreviousRunsAsync();

            var summary = SelectedPreviousRun ?? PreviousRuns.FirstOrDefault();
            if (summary is null)
                return;

            _ = TryLoadSearch(summary);
        }

        private async Task StartPreviousRunAsync(object? parameter)
        {
            if (parameter is not PreviousSearchSummary requestedSummary)
                return;

            await RefreshPreviousRunsAsync();

            var summary = PreviousRuns.FirstOrDefault(r => string.Equals(r.EntryId, requestedSummary.EntryId, StringComparison.Ordinal))
                          ?? PreviousRuns.FirstOrDefault(r => string.Equals(r.RunId, requestedSummary.RunId, StringComparison.Ordinal))
                          ?? requestedSummary;

            if (!TryLoadSearch(summary))
                return;

            await RunSearchAsync();
        }

        private async Task ToggleFavoriteAsync(object? parameter)
        {
            if (parameter is not PreviousSearchSummary summary)
                return;

            if (!_entryIndex.TryGetValue(summary.EntryId, out var context))
            {
                if (!_runIndex.TryGetValue(summary.RunId, out context))
                    return;
            }

            IsBusy = true;
            try
            {
                var runs = context.Hook.Runs;
                if (runs.Count == 0)
                    return;

                var makeFavorite = !summary.IsFavorite;
                var targetId = summary.RunId;
                var index = runs.FindIndex(r => string.Equals(r.RunId, targetId, StringComparison.Ordinal));

                if (index < 0 && summary.FavoriteRunId is not null)
                    index = runs.FindIndex(r => string.Equals(r.RunId, summary.FavoriteRunId, StringComparison.Ordinal));

                if (index < 0)
                    index = runs.FindIndex(r => r.RunUtc == summary.LastRunUtc);

                if (index < 0)
                    index = runs.Count - 1;

                for (var i = 0; i < runs.Count; i++)
                {
                    var source = runs[i];
                    var isFavorite = makeFavorite && i == index;
                    runs[i] = CloneRunWithMetadata(source, source.RunId, isFavorite);
                }

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
            if (parameter is not PreviousSearchSummary summary)
                return;

            if (!_entryIndex.TryGetValue(summary.EntryId, out var context))
            {
                if (!_runIndex.TryGetValue(summary.RunId, out context))
                    return;
            }

            try
            {
                var runs = context.Hook.Runs ?? new List<LitSearchRun>();
                var latest = runs.FirstOrDefault(r => string.Equals(r.RunId, summary.RunId, StringComparison.Ordinal))
                             ?? runs.OrderByDescending(r => r.RunUtc).FirstOrDefault();

                var details = new
                {
                    entryId = context.EntryId,
                    entry = new
                    {
                        context.Entry.Title,
                        Tags = context.Entry.Tags ?? new List<string>()
                    },
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
                        context.Hook.UserNotes,
                        runCount = runs.Count
                    },
                    latestRun = latest is null
                        ? null
                        : new
                        {
                            latest.RunId,
                            latest.From,
                            latest.To,
                            latest.RunUtc,
                            latest.TotalHits,
                            latest.ExecutedBy,
                            latest.IsFavorite,
                            latest.RawAttachments,
                            latest.CheckedEntryIdsPath
                        },
                    runs = runs.Select(r => new
                    {
                        r.RunId,
                        r.From,
                        r.To,
                        r.RunUtc,
                        r.TotalHits,
                        r.ExecutedBy,
                        r.IsFavorite,
                        r.RawAttachments,
                        r.CheckedEntryIdsPath
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(details, JsonStd.Options);
                var tmpPath = Path.Combine(Path.GetTempPath(), $"litsearch_run_{summary.RunId}.json");
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

        private bool TryLoadSearch(PreviousSearchSummary summary)
        {
            if (summary is null)
                return false;

            if (!_entryIndex.TryGetValue(summary.EntryId, out var context))
            {
                if (!_runIndex.TryGetValue(summary.RunId, out context))
                    return false;
            }

            SelectedPreviousRun = summary;

            Query = summary.Query;
            From = summary.LastTo ?? context.Hook.To ?? context.Hook.From ?? summary.LastFrom;
            To = null;
            SelectedDatabase = summary.Provider.Equals("pubmed", StringComparison.OrdinalIgnoreCase)
                ? SearchDatabase.PubMed
                : SearchDatabase.ClinicalTrialsGov;

            _loadedEntryId = context.EntryId;
            _loadedHook = context.Hook;

            Results.Clear();
            return true;
        }

        private async Task RefreshPreviousRunsAsync(CancellationToken ct = default)
        {
            var items = new List<PreviousSearchSummary>();
            _runIndex.Clear();
            _entryIndex.Clear();

            try
            {
                var root = _ws.GetWorkspaceRoot();
                await foreach (var entry in _store.EnumerateAsync(ct))
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var isLitSearchEntry = entry.Type == EntryType.LitSearch
                        || string.Equals(entry.Source, "LitSearch", StringComparison.OrdinalIgnoreCase);
                    if (!isLitSearchEntry)
                        continue;
                    if (string.IsNullOrWhiteSpace(entry.Id))
                        continue;

                    var hookPath = Path.Combine(root, "entries", entry.Id, "hooks", "litsearch.json");
                    if (!File.Exists(hookPath))
                    {
                        var legacyPaths = new[]
                        {
                            Path.Combine(root, "entries", entry.Id, "spokes", "litsearch", "litsearch.json"),
                            Path.Combine(root, "entries", entry.Id, "litsearch", "litsearch.json")
                        };

                        hookPath = legacyPaths.FirstOrDefault(File.Exists);
                        if (hookPath is null)
                            continue;
                    }

                    try
                    {
                        var json = await File.ReadAllTextAsync(hookPath, ct);
                        var hook = JsonSerializer.Deserialize<LitSearchHook>(json, JsonStd.Options);
                        if (hook is null)
                            continue;

                        await HydrateHookNotesAsync(entry.Id!, entry, hook, root, ct);

                        var context = new PreviousSearchContext(entry.Id, hookPath, hook, entry);
                        _entryIndex[context.EntryId] = context;

                        var runs = hook.Runs;
                        var hookUpdated = false;
                        LitSearchRun? latest = null;

                        for (var i = 0; i < runs.Count; i++)
                        {
                            var run = runs[i];
                            if (string.IsNullOrWhiteSpace(run.RunId))
                            {
                                var generatedId = LM.Core.Utils.IdGen.NewId();
                                run = CloneRunWithMetadata(run, generatedId, run.IsFavorite);
                                runs[i] = run;
                                hookUpdated = true;
                            }

                            _runIndex[run.RunId] = context;

                            if (latest is null || run.RunUtc > latest.RunUtc)
                                latest = run;
                        }

                        if (latest is not null)
                        {
                            items.Add(PreviousSearchSummary.Create(context.EntryId, context.Entry, context.Hook, latest));
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
                var favoriteCompare = b.IsFavorite.CompareTo(a.IsFavorite);
                if (favoriteCompare != 0)
                    return favoriteCompare;
                return DateTime.Compare(b.LastRunUtc, a.LastRunUtc);
            });

            var previouslySelected = SelectedPreviousRun;
            var previouslySelectedRunId = previouslySelected?.RunId;
            var previouslySelectedEntryId = previouslySelected?.EntryId;

            PreviousRuns.Clear();
            foreach (var summary in items)
                PreviousRuns.Add(summary);

            if (PreviousRuns.Count == 0)
            {
                SelectedPreviousRun = null;
            }
            else
            {
                var match = previouslySelectedRunId is null
                    ? null
                    : PreviousRuns.FirstOrDefault(r => string.Equals(r.RunId, previouslySelectedRunId, StringComparison.Ordinal));

                if (match is null && previouslySelectedEntryId is not null)
                {
                    match = PreviousRuns.FirstOrDefault(r => string.Equals(r.EntryId, previouslySelectedEntryId, StringComparison.Ordinal));
                }

                if (match is null && previouslySelected is not null)
                {
                    match = PreviousRuns.FirstOrDefault(r =>
                        r.LastRunUtc == previouslySelected.LastRunUtc &&
                        string.Equals(r.Provider, previouslySelected.Provider, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Query, previouslySelected.Query, StringComparison.Ordinal));
                }

                if (match is not null)
                {
                    SelectedPreviousRun = match;
                }
                else if (SelectedPreviousRun is null || !_entryIndex.ContainsKey(SelectedPreviousRun.EntryId))
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

        private static Entry? FindExistingEntry(IReadOnlyList<Entry> entries, SearchHit hit)
        {
            if (entries.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(hit.Doi))
            {
                var doiMatch = entries.FirstOrDefault(e =>
                    !string.IsNullOrWhiteSpace(e.Doi) &&
                    string.Equals(e.Doi, hit.Doi, StringComparison.OrdinalIgnoreCase));
                if (doiMatch is not null)
                    return doiMatch;
            }

            if (!string.IsNullOrWhiteSpace(hit.ExternalId))
            {
                var idMatch = entries.FirstOrDefault(e =>
                    (!string.IsNullOrWhiteSpace(e.Pmid) && string.Equals(e.Pmid, hit.ExternalId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(e.Nct) && string.Equals(e.Nct, hit.ExternalId, StringComparison.OrdinalIgnoreCase)));
                if (idMatch is not null)
                    return idMatch;
            }

            if (!string.IsNullOrWhiteSpace(hit.Title))
            {
                var titleMatch = entries.FirstOrDefault(e =>
                    !string.IsNullOrWhiteSpace(e.Title) &&
                    string.Equals(e.Title, hit.Title, StringComparison.OrdinalIgnoreCase) &&
                    e.Year == hit.Year);
                if (titleMatch is not null)
                    return titleMatch;
            }

            return null;
        }

        private static bool ShouldContinueExisting(LitSearchHook? hook, string? entryId, string currentQuery)
        {
            if (hook is null || string.IsNullOrWhiteSpace(entryId))
                return false;

            return string.Equals(
                NormalizeQueryForComparison(hook.Query),
                NormalizeQueryForComparison(currentQuery),
                StringComparison.Ordinal);
        }

        private static string NormalizeQueryForComparison(string? query)
        {
            if (string.IsNullOrEmpty(query))
                return string.Empty;

            var normalized = query.Replace("\r\n", "\n").Replace('\r', '\n');
            return normalized.TrimEnd();
        }

        private sealed record CheckedEntryIdsSidecar
        {
            [JsonPropertyName("schemaVersion")]
            public string SchemaVersion { get; init; } = "2.0";

            [JsonPropertyName("runId")]
            public string RunId { get; init; } = string.Empty;

            [JsonPropertyName("savedUtc")]
            [JsonConverter(typeof(UtcDateTimeConverter))]
            public DateTime SavedUtc { get; init; }

            [JsonPropertyName("checkedEntries")]
            public CheckedEntriesPayload CheckedEntries { get; init; } = new();
        }

        private sealed record CheckedEntriesPayload
        {
            [JsonPropertyName("entryIds")]
            public List<string> EntryIds { get; init; } = new();
        }

        private static string ComposeSearchNote(string query, string provider, string title, DateTime createdUtc, string? createdBy, int runCount, LitSearchRun latestRun, string? derivedFrom, string? userNotes)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(userNotes))
            {
                sb.AppendLine(userNotes.Trim());
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(title))
                sb.AppendLine($"Title: {title.Trim()}");
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

        private async Task<string?> PersistRunCheckedEntriesAsync(
            LitSearchRunContext runContext,
            string runId,
            IReadOnlyCollection<string> checkedEntryIds,
            DateTime savedUtc)
        {
            if (runContext is null || string.IsNullOrWhiteSpace(runId))
                return null;

            var sidecar = new CheckedEntryIdsSidecar
            {
                RunId = runId,
                SavedUtc = savedUtc,
                CheckedEntries = new CheckedEntriesPayload
                {
                    EntryIds = checkedEntryIds?.ToList() ?? new List<string>()
                }
            };

            var fileName = $"{runContext.FilePrefix}_checked.json";
            var absPath = Path.Combine(runContext.HooksAbsolutePath, fileName);
            await File.WriteAllTextAsync(absPath, JsonSerializer.Serialize(sidecar, JsonStd.Options), Encoding.UTF8);

            var relPath = Path.Combine(runContext.HooksRelativePath, fileName);
            return NormalizeWorkspacePath(relPath);
        }

        private sealed record LitSearchRunContext(string HooksAbsolutePath, string HooksRelativePath, string FilePrefix);
        private sealed record LitSearchRunLibraryFile(string AbsolutePath, string RelativePath);
        private sealed record PubMedImportResult(
            string? RelativeXmlPath,
            IReadOnlyList<string> Authors,
            IReadOnlyList<string> Keywords,
            IReadOnlyList<string> MeshHeadings,
            string? JournalTitle);

        private sealed record PubMedMetadata(
            IReadOnlyList<string> Authors,
            IReadOnlyList<string> Keywords,
            IReadOnlyList<string> MeshHeadings,
            string? JournalTitle);

        private LitSearchRunContext EnsureLitSearchRunContext(string entryId, string runId)
        {
            var context = GetLitSearchRunContext(entryId, runId);
            Directory.CreateDirectory(context.HooksAbsolutePath);
            return context;
        }

        private LitSearchRunContext GetLitSearchRunContext(string entryId, string runId)
        {
            var root = _ws.GetWorkspaceRoot();
            var relative = Path.Combine("entries", entryId, "hooks");
            var absolute = Path.Combine(root, relative);
            var prefix = $"litsearch_run_{runId}";
            return new LitSearchRunContext(absolute, relative, prefix);
        }

        private LitSearchRunLibraryFile EnsureLitSearchRunLibraryFile(string entryId, string runId)
        {
            var hash = ComputeStableHash($"{entryId}:{runId}:records");
            var relativeDir = Path.Combine("library", hash[..2], hash[2..4]);
            var fileName = $"litsearch_{runId}_records.xml";
            var absolute = Path.Combine(_ws.GetWorkspaceRoot(), relativeDir, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            var relative = Path.Combine(relativeDir, fileName);
            return new LitSearchRunLibraryFile(absolute, NormalizeWorkspacePath(relative));
        }

        private static string ComputeStableHash(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string NormalizeWorkspacePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            return path.Replace('\\', '/');
        }

        private async Task<PubMedImportResult?> TrySavePubMedXmlAsync(string litEntryId, string importedEntryId, string runId, string pubmedId)
        {
            try
            {
                var xml = await _pubmed.FetchFullRecordXmlAsync(pubmedId, CancellationToken.None);
                if (string.IsNullOrWhiteSpace(xml))
                    return null;

                var metadata = ExtractPubMedMetadata(XDocument.Parse(xml));
                var libraryFile = EnsureLitSearchRunLibraryFile(litEntryId, runId);
                var document = LoadOrCreateRunXml(libraryFile.AbsolutePath, litEntryId, runId);

                var root = document.Root;
                if (root is null)
                {
                    root = new XElement("litSearchRunData");
                    document.Add(root);
                }

                root.SetAttributeValue("entryId", litEntryId);
                root.SetAttributeValue("searchEntryId", litEntryId);
                root.SetAttributeValue("runId", runId);
                root.SetAttributeValue("updatedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

                var provider = "PubMed";
                var existing = root.Elements("record").FirstOrDefault(e =>
                    string.Equals((string?)e.Attribute("provider"), provider, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)e.Attribute("externalId"), pubmedId, StringComparison.OrdinalIgnoreCase));
                existing?.Remove();

                var record = new XElement("record",
                    new XAttribute("provider", provider),
                    new XAttribute("externalId", pubmedId),
                    new XAttribute("importedEntryId", importedEntryId),
                    new XCData(xml));

                root.Add(record);
                SaveRunXml(document, libraryFile.AbsolutePath);

                return new PubMedImportResult(
                    libraryFile.RelativePath,
                    metadata.Authors,
                    metadata.Keywords,
                    metadata.MeshHeadings,
                    metadata.JournalTitle);
            }
            catch
            {
                return null;
            }
        }

        private static XDocument LoadOrCreateRunXml(string absolutePath, string entryId, string runId)
        {
            if (File.Exists(absolutePath))
            {
                using var stream = File.OpenRead(absolutePath);
                return XDocument.Load(stream);
            }

            var root = new XElement("litSearchRunData",
                new XAttribute("entryId", entryId),
                new XAttribute("runId", runId));
            return new XDocument(root);
        }

        private static void SaveRunXml(XDocument document, string absolutePath)
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
            using var writer = XmlWriter.Create(absolutePath, settings);
            document.Save(writer);
        }

        private static string? NormalizeSource(SearchHit hit)
        {
            if (!string.IsNullOrWhiteSpace(hit.JournalOrSource))
                return hit.JournalOrSource.Trim();
            if (hit.Source == SearchDatabase.ClinicalTrialsGov)
                return "ClinicalTrials.gov";
            return null;
        }

        private static void AddTag(List<string> tagList, HashSet<string> tagSet, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
                return;
            if (tagSet.Add(trimmed))
                tagList.Add(trimmed);
        }

        private static List<string> SplitAuthors(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            var parts = raw
                .Split(new[] { ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parts.Count == 0 && !string.IsNullOrWhiteSpace(raw))
                parts.Add(raw.Trim());

            return parts;
        }

        private static PubMedMetadata ExtractPubMedMetadata(XDocument document)
        {
            var authors = new List<string>();
            var authorSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keywords = new List<string>();
            var keywordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mesh = new List<string>();
            var meshSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? journalTitle = null;

            var article = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Article");
            if (article is not null)
            {
                journalTitle = article
                    .Elements().FirstOrDefault(e => e.Name.LocalName == "Journal")?
                    .Elements().FirstOrDefault(e => e.Name.LocalName == "Title")?
                    .Value?.Trim();

                var authorList = article.Elements().FirstOrDefault(e => e.Name.LocalName == "AuthorList");
                if (authorList is not null)
                {
                    foreach (var author in authorList.Elements().Where(e => e.Name.LocalName == "Author"))
                    {
                        var formatted = FormatPubMedAuthor(author);
                        AddDistinct(authors, authorSet, formatted);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(journalTitle))
            {
                journalTitle = document
                    .Descendants().FirstOrDefault(e => e.Name.LocalName == "MedlineJournalInfo")?
                    .Elements().FirstOrDefault(e => e.Name.LocalName == "MedlineTA")?
                    .Value?.Trim() ?? journalTitle;
            }

            var medlineCitation = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "MedlineCitation");
            if (medlineCitation is not null)
            {
                foreach (var keyword in medlineCitation
                    .Elements().Where(e => e.Name.LocalName == "KeywordList")
                    .SelectMany(list => list.Elements().Where(e => e.Name.LocalName == "Keyword")))
                {
                    AddDistinct(keywords, keywordSet, keyword.Value);
                }
            }

            foreach (var heading in document
                .Descendants().Where(e => e.Name.LocalName == "MeshHeadingList")
                .SelectMany(list => list.Elements().Where(e => e.Name.LocalName == "MeshHeading")))
            {
                var descriptor = heading.Elements().FirstOrDefault(e => e.Name.LocalName == "DescriptorName")?.Value?.Trim();
                var qualifier = heading.Elements().FirstOrDefault(e => e.Name.LocalName == "QualifierName")?.Value?.Trim();
                var term = string.IsNullOrWhiteSpace(qualifier) ? descriptor : $"{descriptor} / {qualifier}";
                AddDistinct(mesh, meshSet, term);
            }

            return new PubMedMetadata(authors, keywords, mesh, journalTitle);
        }

        private static string? FormatPubMedAuthor(XElement author)
        {
            var collective = author.Elements().FirstOrDefault(e => e.Name.LocalName == "CollectiveName")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(collective))
                return collective;

            var last = author.Elements().FirstOrDefault(e => e.Name.LocalName == "LastName")?.Value?.Trim();
            var fore = author.Elements().FirstOrDefault(e => e.Name.LocalName == "ForeName")?.Value?.Trim();
            var initials = author.Elements().FirstOrDefault(e => e.Name.LocalName == "Initials")?.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(last))
            {
                var given = !string.IsNullOrWhiteSpace(fore) ? fore : initials;
                return !string.IsNullOrWhiteSpace(given) ? $"{last}, {given}" : last;
            }

            var literal = author.Elements().FirstOrDefault(e => e.Name.LocalName == "Name")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(literal))
                return literal;

            literal = author.Value?.Trim();
            return string.IsNullOrWhiteSpace(literal) ? null : literal;
        }

        private static void AddDistinct(List<string> target, HashSet<string> seen, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
                return;

            if (seen.Add(trimmed))
                target.Add(trimmed);
        }

        private static List<string> BuildCheckedEntryIds(IEnumerable<SearchHit> hits)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var hit in hits)
            {
                var id = hit.ExistingEntryId;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (seen.Add(id))
                    ordered.Add(id);
            }

            return ordered;
        }

        private async Task PersistHookAsync(PreviousSearchContext context, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(context.Hook, JsonStd.Options);
                Directory.CreateDirectory(Path.GetDirectoryName(context.HookPath)!);
                await File.WriteAllTextAsync(context.HookPath, json, Encoding.UTF8, ct);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private static async Task HydrateHookNotesAsync(string entryId, Entry entry, LitSearchHook hook, string workspaceRoot, CancellationToken ct)
        {
            if (hook is null || string.IsNullOrWhiteSpace(entryId))
                return;

            try
            {
                var notesPath = Path.Combine(workspaceRoot, "entries", entryId, "hooks", "notes.json");
                if (File.Exists(notesPath))
                {
                    var json = await File.ReadAllTextAsync(notesPath, ct);
                    var notesHook = JsonSerializer.Deserialize<EntryNotesHook>(json, JsonStd.Options);
                    if (notesHook is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(notesHook.UserNotes))
                            hook.UserNotes = notesHook.UserNotes;

                        var summaryText = notesHook.SummaryText;
                        if (!string.IsNullOrWhiteSpace(summaryText))
                            hook.NotesSummary = summaryText;

                        entry.UserNotes = hook.UserNotes;
                        entry.Notes = hook.NotesSummary;
                        return;
                    }
                }

                var legacyNotesPath = Path.Combine(workspaceRoot, "entries", entryId, "notes.md");
                if (File.Exists(legacyNotesPath))
                {
                    var text = await File.ReadAllTextAsync(legacyNotesPath, ct);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        hook.NotesSummary = text;
                        hook.UserNotes ??= text;
                        entry.UserNotes = hook.UserNotes;
                        entry.Notes = hook.NotesSummary;
                    }
                }
            }
            catch
            {
                // Ignore hydration issues and continue with hook-only data
            }

            entry.UserNotes ??= hook.UserNotes;
            entry.Notes ??= hook.NotesSummary;
        }

        private void ClearSearchUiAfterSave()
        {
            Query = string.Empty;
            From = null;
            To = null;
            Results.Clear();
            _loadedEntryId = null;
            _loadedHook = null;
            RaiseCanExec();
        }

        private static LitSearchRun CloneRunWithMetadata(LitSearchRun source, string runId, bool isFavorite)
        {
            return new LitSearchRun
            {
                RunId = runId,
                From = source.From,
                To = source.To,
                RunUtc = source.RunUtc,
                TotalHits = source.TotalHits,
                ExecutedBy = source.ExecutedBy,
                CheckedEntryIdsPath = source.CheckedEntryIdsPath,
                RawAttachments = source.RawAttachments is null ? new List<string>() : new List<string>(source.RawAttachments),
                IsFavorite = isFavorite
            };
        }

        private sealed record PreviousSearchContext(string EntryId, string HookPath, LitSearchHook Hook, Entry Entry);

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            return name.Length > 120 ? name[..120] : name;
        }
    }

    public sealed class PreviousSearchSummary
    {
        private PreviousSearchSummary(
            string entryId,
            string runId,
            string provider,
            string displayName,
            string query,
            DateTime? lastFrom,
            DateTime? lastTo,
            DateTime lastRunUtc,
            int totalHits,
            int runCount,
            bool isFavorite,
            string? favoriteRunId,
            IEnumerable<string> tags)
        {
            EntryId = entryId;
            RunId = runId;
            Provider = provider;
            DisplayName = displayName;
            Query = query;
            LastFrom = lastFrom;
            LastTo = lastTo;
            LastRunUtc = lastRunUtc;
            TotalHits = totalHits;
            RunCount = runCount;
            IsFavorite = isFavorite;
            FavoriteRunId = favoriteRunId;
            Tags = tags?.ToList() ?? new List<string>();
        }

        public string EntryId { get; }

        public string RunId { get; }

        public string Provider { get; }

        public string DisplayName { get; }

        public string Query { get; }

        public DateTime? LastFrom { get; }

        public DateTime? LastTo { get; }

        public DateTime LastRunUtc { get; }

        public int TotalHits { get; }

        public int RunCount { get; }

        public bool IsFavorite { get; }

        public string? FavoriteRunId { get; }

        public IReadOnlyList<string> Tags { get; }

        public string TagsDisplay => Tags.Count == 0 ? string.Empty : string.Join(", ", Tags);

        internal static PreviousSearchSummary Create(string entryId, Entry entry, LitSearchHook hook, LitSearchRun latestRun)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));
            if (latestRun is null)
                throw new ArgumentNullException(nameof(latestRun));

            var runs = hook.Runs ?? new List<LitSearchRun>();
            var provider = hook.Provider ?? string.Empty;

            var display = entry.DisplayName;
            if (string.IsNullOrWhiteSpace(display))
                display = hook.Title;
            if (string.IsNullOrWhiteSpace(display))
                display = hook.Query;
            display ??= string.Empty;

            var query = hook.Query ?? string.Empty;

            var favoriteRun = runs.FirstOrDefault(r => r.IsFavorite);
            var runCount = runs.Count == 0 ? 1 : runs.Count;

            return new PreviousSearchSummary(
                entryId,
                latestRun.RunId,
                provider ?? string.Empty,
                display,
                query,
                latestRun.From ?? hook.From,
                latestRun.To ?? hook.To,
                latestRun.RunUtc,
                latestRun.TotalHits,
                runCount,
                favoriteRun is not null,
                favoriteRun?.RunId,
                entry.Tags ?? new List<string>());
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
