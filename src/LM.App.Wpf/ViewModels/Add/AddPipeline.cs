#nullable enable
using DocumentFormat.OpenXml.Bibliography;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Abstractions;
using LM.Infrastructure.Hooks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels
{
    public sealed class AddPipeline : IAddPipeline
    {
        private readonly IEntryStore _store;
        private readonly IFileStorageRepository _storage;
        private readonly IHasher _hasher;
        private readonly ISimilarityService _similarity;
        private readonly IWorkSpaceService _workspace;
        private readonly IMetadataExtractor _metadata;
        private readonly IPublicationLookup _pubs;
        private readonly IDoiNormalizer _doi;
        private readonly IPmidNormalizer _pmid;
        private readonly ISimilarityLog? _simLog = null;
        private readonly HookOrchestrator _orchestrator;

        private static readonly HashSet<string> s_supportedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt", ".md" };

        // ---- Constructors ----------------------------------------------------

        // Compatibility constructor (old call sites) – uses null-object services
        public AddPipeline(IEntryStore store,
                           IFileStorageRepository storage,
                           IHasher hasher,
                           ISimilarityService similarity,
                           IWorkSpaceService workspace,
                           IMetadataExtractor metadata,
                           ISimilarityLog? simLog = null)
            : this(store, storage, hasher, similarity, workspace, metadata,
                   NullPublicationLookup.Instance,
                   NullDoiNormalizer.Instance,
                   new LM.Infrastructure.Hooks.HookOrchestrator(workspace),
                   new NullPmidNormalizer(),
                   simLog)
        { }


        // New DI constructor (recommended)
        public AddPipeline(IEntryStore store,
                           IFileStorageRepository storage,
                           IHasher hasher,
                           ISimilarityService similarity,
                           IWorkSpaceService workspace,
                           IMetadataExtractor metadata,
                           IPublicationLookup publicationLookup,
                           IDoiNormalizer doiNormalizer,
                           LM.Infrastructure.Hooks.HookOrchestrator orchestrator,
                           IPmidNormalizer pmidNormalizer,
                           ISimilarityLog? simLog = null)
        {
            _store = store;
            _storage = storage;
            _hasher = hasher;
            _similarity = similarity;
            _workspace = workspace;
            _metadata = metadata;
            _pubs = publicationLookup;
            _doi = doiNormalizer;
            _orchestrator = orchestrator;
            _pmid = pmidNormalizer;
            _simLog = simLog;
        }


        // ---- Staging & Commit -----------------------------------------------

        public async Task<IReadOnlyList<StagingItem>> StagePathsAsync(IEnumerable<string> paths, CancellationToken ct)
        {
            var items = new List<StagingItem>();
            var ordered = paths.Where(File.Exists)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                               .ToList();

            foreach (var path in ordered)
            {
                if (!IsSupported(path)) continue;
                try
                {
                
                    var item = await StageOneAsync(path, ct);
                    if (item is not null)
                    {  
                        items.Add(item);
                        LM.App.Wpf.Diagnostics.StagingDebugDumper.TryDump(_workspace, item);
                    }
                       
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddPipeline] Failed to stage '{path}': {ex}");
                }
            }
            return items;
        }

        public async Task<IReadOnlyList<StagingItem>> CommitAsync(IEnumerable<StagingItem> selectedRows, CancellationToken ct)
        {
            var committed = new List<StagingItem>();

            // Two-pass commit:
            // 1) Create all NEW/VARIANT/VERSION entries first and remember their ids (so attachments can target them in the same run).
            // 2) Process ATTACHMENT items and append to a resolved target.

            // Materialize once
            var rows = selectedRows?.ToList() ?? new List<StagingItem>();

            // Map freshly created entries so attachments can target them by SimilarToTitle
            var createdByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // ---------- PASS 1: NEW / VARIANT / VERSION ----------
            foreach (var r in rows)
            {
                try
                {
                    var action = (r.SuggestedAction ?? "New").Trim();

                    // Skip rows per rules
                    if (!r.Selected) continue;
                    if (action.Equals("Duplicate", StringComparison.OrdinalIgnoreCase)) continue;
                    if (action.Equals("Skip", StringComparison.OrdinalIgnoreCase)) continue;

                    // Attachments are processed in pass 2
                    if (action.Equals("Attachment", StringComparison.OrdinalIgnoreCase)) continue;

                    // 1) Hash and choose stored name "<hash><ext>"
                    var sha256 = await _hasher.ComputeSha256Async(r.FilePath, ct);
                    var ext = Path.GetExtension(r.FilePath);
                    var storedFileName = $"{sha256}{ext}";

                    // 2) Copy into workspace 'library'
                    var relativePath = await _storage.SaveNewAsync(
                        r.FilePath,
                        relativeTargetDir: "library",
                        preferredFileName: storedFileName,
                        ct: ct);

                    // 3) Build Entry
                    var (title, displayName) = ComputeTitles(r);

                    // Optional relation (non-breaking; stored in tags)
                    var relTags = new List<string>();
                    if (action.Equals("Variant", StringComparison.OrdinalIgnoreCase) || action.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    {
                        var targetId = ResolveTargetEntryId(r.SimilarToEntryId, r.SimilarToTitle, createdByTitle);
                        if (!string.IsNullOrWhiteSpace(targetId))
                        {
                            var rel = action.Equals("Variant", StringComparison.OrdinalIgnoreCase)
                                ? $"rel:variant-of:{targetId}"
                                : $"rel:version-of:{targetId}";
                            relTags.Add(rel);
                        }
                    }

                    var entry = new Entry
                    {
                        Id = string.Empty,
                        Type = r.Type,
                        Title = title,
                        DisplayName = displayName,
                        Year = r.Year,
                        Source = r.Source,
                        Authors = SplitList(r.AuthorsCsv),
                        Doi = r.Doi,
                        Pmid = r.Pmid,
                        IsInternal = r.IsInternal,
                        Tags = SplitList(r.TagsCsv, distinct: true).Concat(relTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        OriginalFileName = Path.GetFileName(r.FilePath),
                        MainFilePath = relativePath,
                        MainFileHashSha256 = sha256
                    };

                    // 4) Persist entry
                    await _store.SaveAsync(entry, ct);

                    // 4b) Ensure we have an id
                    var entryId = entry.Id;
                    if (string.IsNullOrWhiteSpace(entryId))
                    {
                        var saved = await _store.FindByHashAsync(sha256, ct);
                        if (saved is not null) entryId = saved.Id;
                    }

                    // 5) Run composers through orchestrator (article.json now, more later)
                    var ctx = new HookContext { Article = r.ArticleHook };
                    await _orchestrator.ProcessAsync(entryId, ctx, ct);

                    // Track created entry for pass 2 (attachments)
                    var key = r.Title ?? r.DisplayName ?? r.SimilarToTitle;
                    if (!string.IsNullOrWhiteSpace(key))
                        createdByTitle[key!] = entryId;

                    committed.Add(r);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddPipeline] Commit (pass1) failed for '{r.FilePath}': {ex}");
                }
            }

            // ---------- PASS 2: ATTACHMENTS ----------
            foreach (var r in rows)
            {
                try
                {
                    var action = (r.SuggestedAction ?? "New").Trim();
                    if (!action.Equals("Attachment", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!r.Selected) continue;

                    // Resolve target: prefer SimilarToEntryId, otherwise a newly created entry with matching SimilarToTitle
                    var targetId = ResolveTargetEntryId(r.SimilarToEntryId, r.SimilarToTitle, createdByTitle);
                    if (string.IsNullOrWhiteSpace(targetId))
                    {
                        Trace.WriteLine($"[AddPipeline] Attachment has no resolvable target for '{r.FilePath}'. Skipping.");
                        continue;
                    }

                    var target = await _store.GetByIdAsync(targetId!, ct);
                    if (target is null)
                    {
                        Trace.WriteLine($"[AddPipeline] Attachment target '{targetId}' not found for '{r.FilePath}'. Skipping.");
                        continue;
                    }

                    // Save the attachment in /attachments
                    var sha256 = await _hasher.ComputeSha256Async(r.FilePath, ct);
                    var ext = Path.GetExtension(r.FilePath);
                    var storedFileName = $"{sha256}{ext}";

                    var attachmentRel = await _storage.SaveNewAsync(
                        r.FilePath,
                        relativeTargetDir: "attachments",
                        preferredFileName: storedFileName,
                        ct: ct);

                    // Append to target entry (Attachment has only RelativePath in your model)
                    target.Attachments ??= new List<Attachment>();
                    target.Attachments.Add(new Attachment
                    {
                        RelativePath = attachmentRel
                    });

                    // Persist updated target
                    await _store.SaveAsync(target, ct);

                    committed.Add(r);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddPipeline] Commit (pass2/attachment) failed for '{r.FilePath}': {ex}");
                }
            }

            return committed;
        }

        /// <summary>
        /// Resolve the intended target entry for relations (Attachment/Variant/Version).
        /// 1) Use explicit id if available,
        /// 2) else use an entry we created earlier in the same commit with a title matching similarTitle (case-insensitive).
        /// </summary>
        private static string? ResolveTargetEntryId(string? explicitId, string? similarTitle, IDictionary<string, string> createdByTitle)
        {
            if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId;
            if (!string.IsNullOrWhiteSpace(similarTitle) && createdByTitle.TryGetValue(similarTitle!, out var id))
                return id;
            return null;
        }





        // ---- Internals -------------------------------------------------------

        private static bool IsSupported(string path)
            => !string.IsNullOrWhiteSpace(Path.GetExtension(path)) &&
               s_supportedExt.Contains(Path.GetExtension(path));

        private async Task<StagingItem?> StageOneAsync(string path, CancellationToken ct)
        {
            var sessionId = _simLog?.NewSessionId() ?? Guid.NewGuid().ToString("N");

            // 1) Local metadata
            var meta = await ExtractMetaAsync(path, ct);

            // 1b) Authoritative lookup for PDFs with DOI (fast: skip cited-by here)
            PublicationRecord? rec = null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".pdf" && !string.IsNullOrWhiteSpace(meta.Doi))
            {
                try
                {
                    rec = await _pubs.TryGetByDoiAsync(meta.Doi!, includeCitedBy: false, ct);
                    if (rec is not null)
                    {
                        meta.Title = rec.Title ?? meta.Title;
                        meta.Source = rec.JournalTitle ?? meta.Source;
                        meta.Year = rec.Year ?? meta.Year;
                        meta.Pmid = rec.Pmid ?? meta.Pmid;
                        meta.Doi = rec.Doi ?? meta.Doi;
                        meta.TagsCsv = LM.Infrastructure.Utils.TagMerger.Merge(meta.TagsCsv, rec?.Keywords);
                    }
                }
                catch
                {
                    // resilient staging: network hiccups shouldn't block user flow
                }
            }

            // 2) Duplicate by hash
            var sha256 = await _hasher.ComputeSha256Async(path, ct);
            var dup = await _store.FindByHashAsync(sha256, ct);
            if (dup is not null)
            {
                await _simLog.Maybe(sessionId, path, dup.Id, 1.0, "hash", ct);
                var itemDup = BuildItem(path, meta, 1.0, dup.Id, dup.Title ?? dup.DisplayName, MatchKind.Hash);
                PopulateFromRecordOrMeta(itemDup, rec, meta);
                return itemDup;
            }

            // 3) Exact ID match (pdf only)
            Match match = Match.None;
            if (ext == ".pdf")
            {
                match = await TryMatchIdsAsync(sessionId, path, meta, ct);
                if (match.Kind == MatchKind.IdExact)
                {
                    var itemId = BuildItem(path, meta, match.Score, match.EntryId, match.Title, match.Kind);
                    PopulateFromRecordOrMeta(itemId, rec, meta);
                    return itemId;
                }
            }

            // 4) Content similarity
            var best = await ComputeBestContentSimilarityAsync(sessionId, path, ct);
            var item = BuildItem(path, meta, best.Score, best.EntryId, best.Title, best.Kind);
            PopulateFromRecordOrMeta(item, rec, meta);
            return item;
        }

        // ---- Metadata extraction ---------------------------------------------

        private static string? ToCsv(IEnumerable<string>? items)
        {
            if (items == null) return null;
            var list = items.Select(s => s?.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToArray();
            return list.Length == 0 ? null : string.Join(", ", list);
        }

        private async Task<ExtractedMeta> ExtractMetaAsync(string path, CancellationToken ct)
        {
            try
            {
                var m = await _metadata.ExtractAsync(path, ct); // returns FileMetadata
                if (m is null) return new ExtractedMeta();

                return new ExtractedMeta
                {
                    Title = m.Title,
                    AuthorsCsv = ToCsv(m.Authors),
                    Year = m.Year,
                    Source = m.Source,
                    Doi = _doi.Normalize(m.Doi),        // <— use shared normalizer
                    Pmid = _pmid.Normalize(m.Pmid),
                    TagsCsv = ToCsv(m.Tags)
                };
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddPipeline] Metadata extract failed for '{path}': {ex.Message}");
                return new ExtractedMeta();
            }
        }

        // ---- Matching & similarity -------------------------------------------
        private async Task<Match> TryMatchIdsAsync(string sessionId, string path, ExtractedMeta meta, CancellationToken ct)
        {
            var doi = meta.Doi;
            var pmid = meta.Pmid;
            if (string.IsNullOrWhiteSpace(doi) && string.IsNullOrWhiteSpace(pmid))
                return Match.None;

            // Fast path: ask the store (indexed) instead of O(N) enumeration
            Entry? e = null;
            try
            {
                e = await _store.FindByIdsAsync(doi, pmid, ct);
            }
            catch (System.Exception ex)
            {
                // Fall back silently if a store doesn't implement indexing yet
                Trace.WriteLine($"[AddPipeline] FindByIdsAsync failed: {ex.Message}");
            }

            if (e is not null)
            {
                await _simLog.Maybe(sessionId, path, e.Id, 1.0, "id", ct);
                return new Match(e.Id, e.Title ?? e.DisplayName, 1.0, MatchKind.IdExact);
            }

            return Match.None;
        }


        private async Task<Match> ComputeBestContentSimilarityAsync(string sessionId, string path, CancellationToken ct)
        {
            double bestScore = 0.0;
            string? bestId = null;
            string? bestTitle = null;

            await foreach (var e in _store.EnumerateAsync(ct))
            {
                if (string.IsNullOrWhiteSpace(e.MainFilePath)) continue;
                var abs = _workspace.GetAbsolutePath(e.MainFilePath);
                if (string.IsNullOrWhiteSpace(abs) || !File.Exists(abs)) continue;

                double score = 0.0;
                try
                {
                    score = await _similarity.ComputeFileSimilarityAsync(path, abs, ct);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddPipeline] Content sim failed for '{e.Id}': {ex.Message}");
                }

                await _simLog.Maybe(sessionId, path, e.Id, score, "content", ct);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = e.Id;
                    bestTitle = e.Title ?? e.DisplayName;
                }
            }

            return new Match(bestId, bestTitle, bestScore, MatchKind.Content);
        }

        // ---- Item construction & formatting ----------------------------------

        private static StagingItem BuildItem(string path, ExtractedMeta meta, double score, string? id, string? title, MatchKind kind)
        {

            var isDup = kind is MatchKind.Hash or MatchKind.IdExact
            || score >= StagingItem.DuplicateThreshold;
            
            var item = new StagingItem
            {
                Selected = !isDup,
                FilePath = path,
                Type = GuessTypeFromExtension(path),

                Title = meta.Title,
                DisplayName = meta.Title ?? Path.GetFileNameWithoutExtension(path),
                AuthorsCsv = meta.AuthorsCsv,
                Year = meta.Year,
                Source = meta.Source,
                Doi = meta.Doi,
                Pmid = meta.Pmid,
                TagsCsv = meta.TagsCsv,

                Similarity = score,
                SimilarToEntryId = id,
                SimilarToTitle = title,
                SuggestedAction = kind switch
                {
                    MatchKind.Hash => "Duplicate",
                    MatchKind.IdExact => "Duplicate",
                    MatchKind.Content when score >= StagingItem.NearThreshold => "Review",
                    _ => "New"
                }
            };
            return item;
        }

        private static (string Title, string DisplayName) ComputeTitles(StagingItem r)
        {
            var fallbackTitle = Path.GetFileNameWithoutExtension(r.FilePath);
            var title = string.IsNullOrWhiteSpace(r.Title) ? fallbackTitle : r.Title!;
            var display = string.IsNullOrWhiteSpace(r.DisplayName) ? title : r.DisplayName!;
            return (title, display);
        }

        private static List<string> SplitList(string? csv, bool distinct = false)
        {
            var parts = (csv ?? "")
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);

            return (distinct ? parts.Distinct(StringComparer.OrdinalIgnoreCase) : parts).ToList();
        }

        private static EntryType GuessTypeFromExtension(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".ppt" or ".pptx" => EntryType.SlideDeck,
                ".doc" or ".docx" => EntryType.Report,
                ".pdf" => EntryType.Publication,
                _ => EntryType.Other
            };
        }

        private static string FormatYear(int? year) => year is null or 0 ? "n.d." : year.Value.ToString();

        private static string ExtractFirstLastName(string authorsCsv)
        {
            // Accept "Last, First; Last, First" OR "First Last; First Last"
            var firstEntry = authorsCsv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                            ?? authorsCsv;
            var commaIdx = firstEntry.IndexOf(',');
            if (commaIdx > 0) return firstEntry[..commaIdx].Trim();

            var parts = firstEntry.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "" : parts[^1].Trim(',', '.');
        }

        private static void PopulateFromRecordOrMeta(StagingItem item, PublicationRecord? rec, ExtractedMeta meta)
        {
            if (rec is not null && item.Type == EntryType.Publication)
            {
                item.ArticleHook = ArticleHookFactory.CreateFromPublication(rec);
            }

            if (rec?.Keywords is { Count: > 0 })
                item.TagsCsv = LM.Infrastructure.Utils.TagMerger.Merge(item.TagsCsv, rec.Keywords);

            // Prefer authoritative PubMed authors if present
            if (rec is not null && rec.Authors is { Count: > 0 })
            {
                item.AuthorsCsv = rec.AuthorsCsv;

                var first = string.IsNullOrWhiteSpace(rec.FirstAuthorLast) ? "" : rec.FirstAuthorLast;
                var year = rec.Year?.ToString() ?? "n.d.";
                var title = rec.Title ?? item.Title ?? Path.GetFileNameWithoutExtension(item.FilePath);

                item.DisplayName = rec.Authors.Count > 1
                    ? $"{first} et al - {year} - {title}"
                    : $"{first} - {year} - {title}";
                return;
            }

            // Fallback: use whatever the extractor found; do NOT force authors
            if (!string.IsNullOrWhiteSpace(meta.AuthorsCsv))
                item.AuthorsCsv = meta.AuthorsCsv;

            var y = item.Year ?? meta.Year;
            var t = item.Title ?? meta.Title ?? Path.GetFileNameWithoutExtension(item.FilePath);

            if (!string.IsNullOrWhiteSpace(item.AuthorsCsv))
            {
                var firstLast = ExtractFirstLastName(item.AuthorsCsv);
                var hasMultiple = item.AuthorsCsv.Contains(';') || item.AuthorsCsv.Split(',').Length > 2;
                item.DisplayName = hasMultiple
                    ? $"{firstLast} et al - {FormatYear(y)} - {t}"
                    : $"{firstLast} - {FormatYear(y)} - {t}";
            }
            else
            {
                item.DisplayName = $"{FormatYear(y)} - {t}";
            }
        }

        // ---- Helper types ----------------------------------------------------

        private readonly record struct Match(string? EntryId, string? Title, double Score, MatchKind Kind)
        {
            public static Match None => new(null, null, 0.0, MatchKind.None);
        }

        private enum MatchKind { None, Hash, IdExact, Content }

        // Simple DTO used during staging
        private sealed class ExtractedMeta
        {
            public string? Title { get; set; }
            public string? AuthorsCsv { get; set; }
            public int? Year { get; set; }
            public string? Source { get; set; }
            public string? Doi { get; set; }
            public string? Pmid { get; set; }
            public string? TagsCsv { get; set; }
        }

        // Null-objects to keep the compatibility constructor safe
        internal sealed class NullPublicationLookup : IPublicationLookup
        {
            internal static readonly NullPublicationLookup Instance = new();
            public Task<PublicationRecord?> TryGetByDoiAsync(string doi, bool includeCitedBy, CancellationToken ct)
                => Task.FromResult<PublicationRecord?>(null);
        }
        internal sealed class NullPmidNormalizer : IPmidNormalizer
        {
            internal static readonly NullPmidNormalizer Instance = new();
            public string? Normalize(string? raw) => raw?.Trim(); // encourages DI to supply real impl
        }
            internal sealed class NullDoiNormalizer : IDoiNormalizer
        {
            internal static readonly NullDoiNormalizer Instance = new();
            public string? Normalize(string? raw) => raw?.Trim(); // encourage DI registration for real cleaning
        }
    }
}
