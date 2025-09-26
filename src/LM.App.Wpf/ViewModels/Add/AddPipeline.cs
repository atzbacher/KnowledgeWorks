#nullable enable

using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.DataExtraction;
using LM.Core.Utils;
using LM.HubSpoke.Abstractions;
using LM.Infrastructure.Hooks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HookM = LM.HubSpoke.Models;


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
        private readonly IDataExtractionPreprocessor _preprocessor;
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
                   NullDataExtractionPreprocessor.Instance,
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
                           IDataExtractionPreprocessor preprocessor,
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
            _preprocessor = preprocessor ?? NullDataExtractionPreprocessor.Instance;
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

                    var addedBy = GetCurrentUserName();
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
                        InternalId = string.IsNullOrWhiteSpace(r.InternalId) ? null : r.InternalId?.Trim(),
                        IsInternal = r.IsInternal,
                        Tags = SplitList(r.TagsCsv, distinct: true).Concat(relTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        OriginalFileName = Path.GetFileName(r.FilePath),
                        MainFilePath = relativePath,
                        MainFileHashSha256 = sha256,
                        UserNotes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes?.Trim(),
                        AddedBy = addedBy,
                        AddedOnUtc = DateTime.UtcNow
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
                    if (!string.IsNullOrWhiteSpace(entryId))
                    {
                        var ctx = BuildHookContext(r, entry, relativePath, sha256, addedBy);
                        if (ctx is not null)
                        {
                            await _orchestrator.ProcessAsync(entryId!, ctx, ct);
                            if (ctx.Article is not null)
                                r.ArticleHook = ctx.Article;
                        }
                    }

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
                    var now = DateTime.UtcNow;
                    var addedBy = Environment.UserName ?? string.Empty;
                    var title = Path.GetFileNameWithoutExtension(r.FilePath) ?? string.Empty;
                    target.Attachments.Add(new Attachment
                    {
                        RelativePath = attachmentRel,
                        Title = string.IsNullOrWhiteSpace(title) ? attachmentRel : title!,
                        Kind = AttachmentKind.Supplement,
                        Tags = new List<string>(),
                        AddedBy = addedBy,
                        AddedUtc = now
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
                    await TryPopulateEvidenceAsync(itemId, path, sha256, ext, ct).ConfigureAwait(false);
                    return itemId;
                }
            }

            // 4) Content similarity
            var best = await ComputeBestContentSimilarityAsync(sessionId, path, meta, ct);
            var item = BuildItem(path, meta, best.Score, best.EntryId, best.Title, best.Kind);
            PopulateFromRecordOrMeta(item, rec, meta);
            await TryPopulateEvidenceAsync(item, path, sha256, ext, ct).ConfigureAwait(false);
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


        private async Task<Match> ComputeBestContentSimilarityAsync(string sessionId, string path, ExtractedMeta meta, CancellationToken ct)
        {
            double bestScore = 0.0;
            string? bestId = null;
            string? bestTitle = null;
            var compared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            async Task EvaluateAsync(Entry entry)
            {
                if (string.IsNullOrWhiteSpace(entry.MainFilePath))
                    return;

                var abs = _workspace.GetAbsolutePath(entry.MainFilePath);
                if (string.IsNullOrWhiteSpace(abs) || !File.Exists(abs))
                    return;

                double score;
                try
                {
                    score = await _similarity.ComputeFileSimilarityAsync(path, abs, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddPipeline] Content sim failed for '{entry.Id}': {ex.Message}");
                    return;
                }

                await _simLog.Maybe(sessionId, path, entry.Id, score, "content", ct).ConfigureAwait(false);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = entry.Id;
                    bestTitle = entry.Title ?? entry.DisplayName;
                }
            }

            IReadOnlyList<Entry>? candidates = null;
            if (!string.IsNullOrWhiteSpace(meta.Title))
            {
                try
                {
                    candidates = await _store.FindSimilarByNameYearAsync(meta.Title!, meta.Year, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddPipeline] FindSimilarByNameYearAsync failed: {ex.Message}");
                }
            }

            if (candidates is not null && candidates.Count > 0)
            {
                foreach (var entry in candidates)
                {
                    if (!string.IsNullOrEmpty(entry.Id))
                        compared.Add(entry.Id);

                    await EvaluateAsync(entry).ConfigureAwait(false);

                    if (bestScore >= StagingItem.DuplicateThreshold)
                        return new Match(bestId, bestTitle, bestScore, MatchKind.Content);
                }

                if (bestScore >= StagingItem.NearThreshold)
                    return new Match(bestId, bestTitle, bestScore, MatchKind.Content);
            }

            await foreach (var entry in _store.EnumerateAsync(ct).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(entry.Id) && compared.Contains(entry.Id))
                    continue;

                await EvaluateAsync(entry).ConfigureAwait(false);

                if (bestScore >= StagingItem.DuplicateThreshold)
                    break;
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

        private async Task TryPopulateEvidenceAsync(StagingItem item, string path, string sha256, string extension, CancellationToken ct)
        {
            if (item is null)
                throw new ArgumentNullException(nameof(item));
            if (item.IsDuplicate)
                return;
            if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var request = new DataExtractionPreprocessRequest(path)
                {
                    PreferredCacheKey = sha256
                };

                var result = await _preprocessor.PreprocessAsync(request, ct).ConfigureAwait(false);
                if (result is null || result.IsEmpty)
                    return;

                item.EvidencePreview = BuildEvidencePreview(result);
                item.DataExtractionHook = BuildDataExtractionHook(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddPipeline] Data extraction preprocessing failed for '{path}': {ex.Message}");
            }
        }

        private static StagingEvidencePreview BuildEvidencePreview(DataExtractionPreprocessResult result)
        {
            var sections = result.Sections.Select(s => new StagingEvidencePreview.SectionPreview
            {
                Heading = s.Heading,
                Body = Truncate(s.Body, 800),
                Pages = s.PageNumbers
            }).ToList();

            var tables = result.Tables.Select(t => new StagingEvidencePreview.TablePreview
            {
                Title = string.IsNullOrWhiteSpace(t.Title) ? "Table" : t.Title,
                Classification = t.Classification,
                Populations = t.DetectedPopulations,
                Endpoints = t.DetectedEndpoints,
                Pages = t.PageNumbers
            }).ToList();

            var figures = result.Figures.Select(f => new StagingEvidencePreview.FigurePreview
            {
                Caption = f.Caption,
                Pages = f.PageNumbers,
                ThumbnailPath = NormalizeWorkspacePath(f.ThumbnailRelativePath)
            }).ToList();

            return new StagingEvidencePreview
            {
                Sections = sections,
                Tables = tables,
                Figures = figures,
                Provenance = result.Provenance
            };
        }

        private HookM.DataExtractionHook BuildDataExtractionHook(DataExtractionPreprocessResult result)
        {
            var populations = new Dictionary<string, HookM.DataExtractionPopulation>(StringComparer.OrdinalIgnoreCase);
            var interventions = new Dictionary<string, HookM.DataExtractionIntervention>(StringComparer.OrdinalIgnoreCase);
            var endpoints = new Dictionary<string, HookM.DataExtractionEndpoint>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in result.Tables)
            {
                foreach (var pop in table.DetectedPopulations)
                {
                    if (string.IsNullOrWhiteSpace(pop))
                        continue;
                    if (!populations.ContainsKey(pop))
                    {
                        populations[pop] = new HookM.DataExtractionPopulation
                        {
                            Label = pop
                        };
                    }
                }

                foreach (var endpoint in table.DetectedEndpoints)
                {
                    if (string.IsNullOrWhiteSpace(endpoint))
                        continue;
                    if (!endpoints.ContainsKey(endpoint))
                    {
                        endpoints[endpoint] = new HookM.DataExtractionEndpoint
                        {
                            Name = endpoint
                        };
                    }
                }

                foreach (var column in table.Columns.Where(c => c.Role == TableColumnRole.Intervention))
                {
                    var label = string.IsNullOrWhiteSpace(column.Header) ? column.NormalizedHeader : column.Header;
                    if (string.IsNullOrWhiteSpace(label))
                        continue;
                    if (!interventions.ContainsKey(label))
                    {
                        interventions[label] = new HookM.DataExtractionIntervention
                        {
                            Name = label
                        };
                    }
                }
            }

            foreach (var intervention in interventions.Values)
            {
                foreach (var population in populations.Values)
                {
                    if (intervention.Name.Contains(population.Label, StringComparison.OrdinalIgnoreCase))
                    {
                        intervention.PopulationIds.Add(population.Id);
                    }
                }
            }

            var tableHooks = new List<HookM.DataExtractionTable>();
            foreach (var table in result.Tables)
            {
                var linkedEndpoints = table.DetectedEndpoints
                    .Where(e => endpoints.ContainsKey(e))
                    .Select(e => endpoints[e].Id)
                    .Distinct()
                    .ToList();

                var linkedInterventions = table.Columns
                    .Where(c => c.Role == TableColumnRole.Intervention)
                    .Select(c => string.IsNullOrWhiteSpace(c.Header) ? c.NormalizedHeader : c.Header)
                    .Where(label => !string.IsNullOrWhiteSpace(label) && interventions.ContainsKey(label))
                    .Select(label => interventions[label!].Id)
                    .Distinct()
                    .ToList();

                var hookTable = new HookM.DataExtractionTable
                {
                    Title = string.IsNullOrWhiteSpace(table.Title) ? "Table" : table.Title,
                    Caption = table.Classification.ToString(),
                    SourcePath = NormalizeWorkspacePath(table.CsvRelativePath),
                    Pages = table.PageNumbers.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToList(),
                    ProvenanceHash = FormatProvenanceHash(table.ProvenanceHash)
                };

                hookTable.LinkedEndpointIds.AddRange(linkedEndpoints);
                hookTable.LinkedInterventionIds.AddRange(linkedInterventions);
                tableHooks.Add(hookTable);
            }

            var figureHooks = result.Figures.Select(f => new HookM.DataExtractionFigure
            {
                Title = string.IsNullOrWhiteSpace(f.Caption) ? "Figure" : f.Caption,
                Caption = f.Caption,
                SourcePath = NormalizeWorkspacePath(f.ThumbnailRelativePath),
                Pages = f.PageNumbers.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToList(),
                ProvenanceHash = FormatProvenanceHash(f.ProvenanceHash)
            }).ToList();

            return new HookM.DataExtractionHook
            {
                ExtractedAtUtc = result.Provenance.ExtractedAtUtc,
                ExtractedBy = string.IsNullOrWhiteSpace(result.Provenance.ExtractedBy) ? GetCurrentUserName() : result.Provenance.ExtractedBy,
                Populations = populations.Values.ToList(),
                Interventions = interventions.Values.ToList(),
                Endpoints = endpoints.Values.ToList(),
                Figures = figureHooks,
                Tables = tableHooks,
                Notes = null
            };
        }

        private async Task TryPopulateEvidenceAsync(StagingItem item, string path, string sha256, string extension, CancellationToken ct)
        {
            if (item is null)
                throw new ArgumentNullException(nameof(item));
            if (item.IsDuplicate)
                return;
            if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var request = new DataExtractionPreprocessRequest(path)
                {
                    PreferredCacheKey = sha256
                };

                var result = await _preprocessor.PreprocessAsync(request, ct).ConfigureAwait(false);
                if (result is null || result.IsEmpty)
                    return;

                item.EvidencePreview = BuildEvidencePreview(result);
                item.DataExtractionHook = BuildDataExtractionHook(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddPipeline] Data extraction preprocessing failed for '{path}': {ex.Message}");
            }
        }

        private static StagingEvidencePreview BuildEvidencePreview(DataExtractionPreprocessResult result)
        {
            var sections = result.Sections.Select(s => new StagingEvidencePreview.SectionPreview
            {
                Heading = s.Heading,
                Body = Truncate(s.Body, 800),
                Pages = s.PageNumbers
            }).ToList();

            var tables = result.Tables.Select(t => new StagingEvidencePreview.TablePreview
            {
                Title = string.IsNullOrWhiteSpace(t.Title) ? "Table" : t.Title,
                Classification = t.Classification,
                Populations = t.DetectedPopulations,
                Endpoints = t.DetectedEndpoints,
                Pages = t.PageNumbers
            }).ToList();

            var figures = result.Figures.Select(f => new StagingEvidencePreview.FigurePreview
            {
                Caption = f.Caption,
                Pages = f.PageNumbers,
                ThumbnailPath = NormalizeWorkspacePath(f.ThumbnailRelativePath)
            }).ToList();

            return new StagingEvidencePreview
            {
                Sections = sections,
                Tables = tables,
                Figures = figures,
                Provenance = result.Provenance
            };
        }

        private HookM.DataExtractionHook BuildDataExtractionHook(DataExtractionPreprocessResult result)
        {
            var populations = new Dictionary<string, HookM.DataExtractionPopulation>(StringComparer.OrdinalIgnoreCase);
            var interventions = new Dictionary<string, HookM.DataExtractionIntervention>(StringComparer.OrdinalIgnoreCase);
            var endpoints = new Dictionary<string, HookM.DataExtractionEndpoint>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in result.Tables)
            {
                foreach (var pop in table.DetectedPopulations)
                {
                    if (string.IsNullOrWhiteSpace(pop))
                        continue;
                    if (!populations.ContainsKey(pop))
                    {
                        populations[pop] = new HookM.DataExtractionPopulation
                        {
                            Label = pop
                        };
                    }
                }

                foreach (var endpoint in table.DetectedEndpoints)
                {
                    if (string.IsNullOrWhiteSpace(endpoint))
                        continue;
                    if (!endpoints.ContainsKey(endpoint))
                    {
                        endpoints[endpoint] = new HookM.DataExtractionEndpoint
                        {
                            Name = endpoint
                        };
                    }
                }

                foreach (var column in table.Columns.Where(c => c.Role == TableColumnRole.Intervention))
                {
                    var label = string.IsNullOrWhiteSpace(column.Header) ? column.NormalizedHeader : column.Header;
                    if (string.IsNullOrWhiteSpace(label))
                        continue;
                    if (!interventions.ContainsKey(label))
                    {
                        interventions[label] = new HookM.DataExtractionIntervention
                        {
                            Name = label
                        };
                    }
                }
            }

            foreach (var intervention in interventions.Values)
            {
                foreach (var population in populations.Values)
                {
                    if (intervention.Name.Contains(population.Label, StringComparison.OrdinalIgnoreCase))
                    {
                        intervention.PopulationIds.Add(population.Id);
                    }
                }
            }

            var tableHooks = new List<HookM.DataExtractionTable>();
            foreach (var table in result.Tables)
            {
                var linkedEndpoints = table.DetectedEndpoints
                    .Where(e => endpoints.ContainsKey(e))
                    .Select(e => endpoints[e].Id)
                    .Distinct()
                    .ToList();

                var linkedInterventions = table.Columns
                    .Where(c => c.Role == TableColumnRole.Intervention)
                    .Select(c => string.IsNullOrWhiteSpace(c.Header) ? c.NormalizedHeader : c.Header)
                    .Where(label => !string.IsNullOrWhiteSpace(label) && interventions.ContainsKey(label))
                    .Select(label => interventions[label!].Id)
                    .Distinct()
                    .ToList();

                var hookTable = new HookM.DataExtractionTable
                {
                    Title = string.IsNullOrWhiteSpace(table.Title) ? "Table" : table.Title,
                    Caption = table.Classification.ToString(),
                    SourcePath = NormalizeWorkspacePath(table.CsvRelativePath),
                    Pages = table.PageNumbers.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToList(),
                    ProvenanceHash = FormatProvenanceHash(table.ProvenanceHash)
                };

                hookTable.LinkedEndpointIds.AddRange(linkedEndpoints);
                hookTable.LinkedInterventionIds.AddRange(linkedInterventions);
                tableHooks.Add(hookTable);
            }

            var figureHooks = result.Figures.Select(f => new HookM.DataExtractionFigure
            {
                Title = string.IsNullOrWhiteSpace(f.Caption) ? "Figure" : f.Caption,
                Caption = f.Caption,
                SourcePath = NormalizeWorkspacePath(f.ThumbnailRelativePath),
                Pages = f.PageNumbers.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToList(),
                ProvenanceHash = FormatProvenanceHash(f.ProvenanceHash)
            }).ToList();

            return new HookM.DataExtractionHook
            {
                ExtractedAtUtc = result.Provenance.ExtractedAtUtc,
                ExtractedBy = string.IsNullOrWhiteSpace(result.Provenance.ExtractedBy) ? GetCurrentUserName() : result.Provenance.ExtractedBy,
                Populations = populations.Values.ToList(),
                Interventions = interventions.Values.ToList(),
                Endpoints = endpoints.Values.ToList(),
                Figures = figureHooks,
                Tables = tableHooks,
                Notes = null
            };
        }

        private static (string Title, string DisplayName) ComputeTitles(StagingItem r)
        {
            var fallbackTitle = Path.GetFileNameWithoutExtension(r.FilePath);
            var title = string.IsNullOrWhiteSpace(r.Title) ? fallbackTitle : r.Title!;
            var display = string.IsNullOrWhiteSpace(r.DisplayName) ? title : r.DisplayName!;
            return (title, display);
        }


        private HookContext? BuildHookContext(StagingItem stagingItem,
                                              Entry entry,
                                              string relativePath,
                                              string sha256,
                                              string addedBy)
        {
            if (stagingItem is null)
                throw new ArgumentNullException(nameof(stagingItem));
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            var article = BuildArticleHook(stagingItem, entry, relativePath, sha256);
            var changeLog = BuildEntryCreationChangeLog(entry, addedBy);

            if (article is null && changeLog is null)
                return null;

            return new HookContext
            {
                Article = article,
                ChangeLog = changeLog,
                DataExtraction = stagingItem.DataExtractionHook
            };
        }

        private HookM.ArticleHook? BuildArticleHook(StagingItem stagingItem,
                                                    Entry entry,
                                                    string relativePath,
                                                    string sha256)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return stagingItem.ArticleHook;

            var normalized = NormalizeStoragePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalized))
                return stagingItem.ArticleHook;

            var hook = stagingItem.ArticleHook ?? CreateMinimalArticleHook(entry);

            var asset = CreatePrimaryAsset(normalized, stagingItem.FilePath, sha256);
            if (asset is null)
                return hook;

            hook.Assets.RemoveAll(a => a?.Purpose == HookM.ArticleAssetPurpose.Manuscript);
            hook.Assets.Add(asset);
            return hook;
        }

        private static HookM.ArticleHook CreateMinimalArticleHook(Entry entry)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            var authors = (entry.Authors ?? new List<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => new HookM.Author { LastName = a.Trim() })
                .ToList();

            var keywords = (entry.Tags ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList();

            return new HookM.ArticleHook
            {
                Identifier = new HookM.ArticleIdentifier
                {
                    PMID = entry.Pmid ?? string.Empty,
                    DOI = entry.Doi,
                    OtherIds = new Dictionary<string, string>()
                },
                Journal = new HookM.JournalInfo
                {
                    Title = entry.Source ?? string.Empty,
                    Issue = new HookM.JournalIssue
                    {
                        PubDate = entry.Year.HasValue
                            ? new HookM.PartialDate { Year = entry.Year.Value }
                            : null
                    }
                },
                Article = new HookM.ArticleDetails
                {
                    Title = entry.Title ?? entry.DisplayName ?? entry.OriginalFileName ?? string.Empty,
                    PublicationTypes = new List<string>(),
                    Pagination = new HookM.Pagination(),
                    Dates = new HookM.ArticleDates()
                },
                Authors = authors,
                Keywords = keywords
            };
        }

        private HookM.ArticleAsset? CreatePrimaryAsset(string storagePath, string originalPath, string sha256)
        {
            try
            {
                var bytes = TryGetFileSize(storagePath);
                var title = Path.GetFileName(storagePath) ?? storagePath;
                var originalFile = string.IsNullOrWhiteSpace(originalPath) ? null : Path.GetFileName(originalPath);
                var originalFolder = string.IsNullOrWhiteSpace(originalPath) ? null : Path.GetDirectoryName(originalPath);

                return new HookM.ArticleAsset
                {
                    Title = string.IsNullOrWhiteSpace(title) ? storagePath : title,
                    OriginalFilename = originalFile,
                    OriginalFolderPath = originalFolder,
                    StoragePath = storagePath,
                    Hash = string.IsNullOrWhiteSpace(sha256) ? string.Empty : $"sha256-{sha256}",
                    ContentType = ResolveContentType(storagePath),
                    Bytes = bytes,
                    Purpose = HookM.ArticleAssetPurpose.Manuscript
                };
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddPipeline] Failed to build primary asset for '{storagePath}': {ex}");
                return null;
            }
        }

        private HookM.EntryChangeLogHook? BuildEntryCreationChangeLog(Entry entry, string addedBy)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            var normalized = NormalizeStoragePath(entry.MainFilePath);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var performer = string.IsNullOrWhiteSpace(addedBy) ? "unknown" : addedBy;
            var timestamp = entry.AddedOnUtc == default ? DateTime.UtcNow : entry.AddedOnUtc;

            var title = entry.DisplayName;
            if (string.IsNullOrWhiteSpace(title))
                title = entry.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = entry.OriginalFileName;
            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(title))
                title = entry.Id;

            var tags = entry.Tags is { Count: > 0 }
                ? new List<string>(entry.Tags)
                : new List<string>();

            return new HookM.EntryChangeLogHook
            {
                Events = new List<HookM.EntryChangeLogEvent>
                {
                    new HookM.EntryChangeLogEvent
                    {
                        EventId = Guid.NewGuid().ToString("N"),
                        TimestampUtc = timestamp,
                        PerformedBy = performer,
                        Action = "EntryCreated",
                        Details = new HookM.ChangeLogAttachmentDetails
                        {
                            AttachmentId = entry.Id,
                            Title = title,
                            LibraryPath = normalized,
                            Purpose = AttachmentKind.Supplement,
                            Tags = tags
                        }
                    }
                }
            };
        }

        private long TryGetFileSize(string storagePath)
        {
            try
            {
                var relative = storagePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);

                var absolute = _workspace.GetAbsolutePath(relative);
                if (!string.IsNullOrWhiteSpace(absolute))
                {
                    var info = new FileInfo(absolute);
                    if (info.Exists)
                        return info.Length;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddPipeline] Unable to determine file size for '{storagePath}': {ex}");
            }

            return 0;
        }

        private static string NormalizeStoragePath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            return relativePath.Replace('\\', '/');
        }

        private static string NormalizeWorkspacePath(string? relativePath)
            => NormalizeStoragePath(relativePath);

        private static string FormatProvenanceHash(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            var normalized = hash.Trim();
            return normalized.StartsWith("sha256-", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"sha256-{normalized.ToLowerInvariant()}";
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Length <= maxLength
                ? value
                : string.Concat(value.AsSpan(0, maxLength), '…');
        }

        private static string ResolveContentType(string storagePath)
        {
            var ext = Path.GetExtension(storagePath)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".rtf" => "application/rtf",
                _ => "application/octet-stream"
            };
        }

        private static string GetCurrentUserName()
        {
            var user = Environment.UserName;
            return string.IsNullOrWhiteSpace(user) ? "unknown" : user;
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
