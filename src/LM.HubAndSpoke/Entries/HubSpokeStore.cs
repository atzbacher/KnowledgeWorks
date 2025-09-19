#nullable enable
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Core.Utils;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.FileSystem;
using LM.HubSpoke.Hubs;
using LM.HubSpoke.Indexing;
using LM.HubSpoke.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.HubSpoke.Models;

namespace LM.HubSpoke.Entries
{
    public sealed class HubSpokeStore : IEntryStore
    {
        private readonly IWorkSpaceService _ws;
        private readonly ContentStore _content;
        private readonly IContentExtractor? _extract;
        private readonly SqliteSearchIndex _index;
        private readonly Dictionary<EntryType, ISpokeHandler> _handlers;

        private readonly IdIndex _idIndex;
        private volatile bool _idIndexBuilt = false;
        private readonly SemaphoreSlim _idIndexLock = new(1, 1);

        // BACK-COMPAT CTOR (old signature) — callers can keep using this.
        // It forwards to the internal ctor with minimal fallback normalizers.
        public HubSpokeStore(
            IWorkSpaceService ws,
            IHasher hasher,
            IEnumerable<ISpokeHandler> handlers,
            IContentExtractor? contentExtractor = null)
            : this(
                ws, hasher, handlers,
                normDoi: FallbackNormalizeDoi,
                normPmid: FallbackNormalizePmid,
                contentExtractor: contentExtractor)
        { }

        // PREFERRED CTOR — inject shared normalizers from composition root
        public HubSpokeStore(
            IWorkSpaceService ws,
            IHasher hasher,
            IEnumerable<ISpokeHandler> handlers,
            IDoiNormalizer doiNormalizer,
            IPmidNormalizer pmidNormalizer,
            IContentExtractor? contentExtractor = null)
            : this(
                ws, hasher, handlers,
                normDoi: (doiNormalizer ?? throw new ArgumentNullException(nameof(doiNormalizer))).Normalize,
                normPmid: (pmidNormalizer ?? throw new ArgumentNullException(nameof(pmidNormalizer))).Normalize,
                contentExtractor: contentExtractor)
        { }

        // INTERNAL CTOR — single place for initialization
        private HubSpokeStore(
            IWorkSpaceService ws,
            IHasher hasher,
            IEnumerable<ISpokeHandler> handlers,
            Func<string?, string?> normDoi,
            Func<string?, string?> normPmid,
            IContentExtractor? contentExtractor)
        {
            _ws = ws;
            _content = new ContentStore(ws, hasher);
            _extract = contentExtractor;
            _index = new SqliteSearchIndex(ws);
            _handlers = handlers.ToDictionary(h => h.Handles);
            _idIndex = new IdIndex(normDoi, normPmid);
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            WorkspaceLayout.Ensure(_ws);
            await _index.InitializeAsync(ct);
        }

        public async Task SaveAsync(Entry entry, CancellationToken ct = default)
        {
            var isNew = string.IsNullOrWhiteSpace(entry.Id);
            var entryId = isNew ? IdGen.NewId() : entry.Id!;
            var now = DateTime.UtcNow;

            // 1) CAS primary
            var primary = string.IsNullOrWhiteSpace(entry.MainFilePath)
                ? new CasResult(null, null, 0, null, entry.OriginalFileName)
                : await _content.MoveToCasAsync(_ws.GetAbsolutePath(entry.MainFilePath!), ct);

            // reflect CAS back
            if (primary.RelPath is not null) entry.MainFilePath = primary.RelPath;
            if (primary.Sha is not null) entry.MainFileHashSha256 = primary.Sha;
            if (string.IsNullOrWhiteSpace(entry.OriginalFileName) && primary.Original is not null)
                entry.OriginalFileName = primary.Original;

            // 2) Hub
            var isPublication = entry.Type == EntryType.Publication;
            var isLitSearch = string.Equals(entry.Source, "LitSearch", StringComparison.OrdinalIgnoreCase);
            var createdBy = BuildPersonRef(entry.AddedBy);
            var hasNotes = !string.IsNullOrWhiteSpace(entry.Notes) || !string.IsNullOrWhiteSpace(entry.UserNotes);

            var hub = new EntryHub
            {
                EntryId = entryId,
                DisplayTitle = entry.Title ?? entry.DisplayName ?? string.Empty,
                CreatedUtc = entry.AddedOnUtc == default ? now : entry.AddedOnUtc,
                UpdatedUtc = now,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
                CreationMethod = isLitSearch ? CreationMethod.Search : CreationMethod.Manual,
                Origin = entry.IsInternal ? EntryOrigin.Internal : EntryOrigin.External,
                PrimaryPurpose = isPublication ? EntryPurpose.Manuscript : EntryPurpose.Document,
                PrimaryPurposeSource = PurposeSource.Inferred,
                Tags = entry.Tags ?? new List<string>(),
                Hooks = new EntryHooks
                {
                    Article = isPublication ? "hooks/article.json" : null,
                    Document = !isPublication && !isLitSearch ? "hooks/document.json" : null,
                    LitSearch = isLitSearch ? "hooks/litsearch.json" : null,
                    Notes = hasNotes ? "hooks/notes.json" : null
                }
            };
            await HubJsonStore.SaveAsync(_ws, hub, ct);

            var entryDir = WorkspaceLayout.EntryDir(_ws, entryId);
            var notesPath = WorkspaceLayout.NotesHookPath(_ws, entryId);
            if (hasNotes)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(notesPath)!);
                var notesHook = new EntryNotesHook
                {
                    Summary = entry.Notes,
                    UserNotes = entry.UserNotes,
                    UpdatedUtc = now
                };
                await File.WriteAllTextAsync(notesPath, JsonSerializer.Serialize(notesHook, JsonStd.Options), ct);
            }
            else if (File.Exists(notesPath))
            {
                File.Delete(notesPath);
            }

            // 3) Spoke
            if (!_handlers.TryGetValue(entry.Type, out var handler))
                handler = _handlers.ContainsKey(EntryType.Report) ? _handlers[EntryType.Report] : _handlers.Values.First();

            var attachmentRelPaths = (entry.Attachments ?? new List<Attachment>()).Select(a => a.RelativePath);
            var hook = await handler.BuildHookAsync(entry, primary, attachmentRelPaths, MoveToCasFromRelAsync, ct);

            // write hook
            var hookPath = Path.Combine(WorkspaceLayout.EntryDir(_ws, entryId), handler.HookPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(hookPath)!);
            await File.WriteAllTextAsync(hookPath, JsonSerializer.Serialize(hook, JsonStd.Options), ct);

            // 4) Extract text (optional)
            string? extracted = null;
            if (_extract is not null && primary.RelPath is not null)
            {
                try { extracted = await _extract.ExtractTextAsync(_ws.GetAbsolutePath(primary.RelPath), ct); } catch { }
            }

            // 5) Index
            var contrib = handler.BuildIndex(hub, hook, extracted);
            await _index.UpsertAsync(new SqliteSearchIndex.IndexRecord(
                EntryId: entryId,
                DisplayTitle: hub.DisplayTitle,
                Year: contrib.Year,
                IsInternal: hub.Origin == EntryOrigin.Internal,
                Type: entry.Type.ToString(),
                Doi: contrib.Doi,
                Pmid: contrib.Pmid,
                Journal: contrib.Journal,
                Title: contrib.Title,
                Abstract: contrib.Abstract,
                Authors: contrib.Authors,
                Keywords: contrib.Keywords,
                Tags: hub.Tags?.ToList() ?? new List<string>(),
                AssetHashes: contrib.AssetHashes,
                Content: contrib.FullText
            ), ct);

            entry.Id = entryId;

            // update in-memory ID index
            _idIndex.AddOrUpdate(entry.Doi, entry.Pmid, entryId);
        }

        private static PersonRef BuildPersonRef(string? user)
        {
            if (string.IsNullOrWhiteSpace(user))
                return PersonRef.Unknown;

            var trimmed = user.Trim();
            return new PersonRef(trimmed, trimmed);
        }

        public async Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
        {
            await EnsureIdIndexAsync(ct);
            var id = _idIndex.Find(doi, pmid);
            return id is null ? null : await GetByIdAsync(id, ct);
        }

        private async Task EnsureIdIndexAsync(CancellationToken ct)
        {
            if (_idIndexBuilt) return;
            await _idIndexLock.WaitAsync(ct);
            try
            {
                if (_idIndexBuilt) return;

                // Build from existing entries once (lazy, at first lookup).
                var root = WorkspaceLayout.EntriesRoot(_ws);
                if (Directory.Exists(root))
                {
                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        if (ct.IsCancellationRequested) break;
                        var id = Path.GetFileName(dir);
                        var e = await GetByIdAsync(id, ct);
                        if (e is not null)
                            _idIndex.AddOrUpdate(e.Doi, e.Pmid, id);
                    }
                }
                _idIndexBuilt = true;
            }
            finally
            {
                _idIndexLock.Release();
            }
        }

        public async Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var hub = await Hubs.HubJsonStore.LoadAsync(_ws, id, ct);
            if (hub is null) return null;

            var handler = PickHandler(hub);
            var hook = await handler.LoadHookAsync(_ws, id, ct);
            return handler.MapToEntry(hub, hook);
        }

        public async Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default)
        {
            var hits = await _index.SearchAsync(filter, ct);
            var list = new List<Entry>(hits.Count);
            foreach (var h in hits)
            {
                var e = await GetByIdAsync(h.EntryId, ct);
                if (e is not null) list.Add(e);
            }
            return list;
        }

        public async IAsyncEnumerable<Entry> EnumerateAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            var root = WorkspaceLayout.EntriesRoot(_ws);
            if (!Directory.Exists(root)) yield break;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (ct.IsCancellationRequested) yield break;
                var id = Path.GetFileName(dir);
                var e = await GetByIdAsync(id, ct);
                if (e is not null) yield return e;
            }
        }

        public async Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
        {
            var ids = await _index.FindByHashAsync(sha256, ct);
            foreach (var id in ids)
            {
                var e = await GetByIdAsync(id, ct);
                if (e is not null) return e;
            }
            return null;
        }

        public async Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
        {
            // Narrow by title and (optionally) year, then reuse the normal search path
            var f = new EntryFilter
            {
                TitleContains = string.IsNullOrWhiteSpace(title) ? null : title,
                YearFrom = year,
                YearTo = year
            };
            return await SearchAsync(f, ct);
        }

        // ---- helpers ----
        private ISpokeHandler PickHandler(EntryHub hub)
        {
            // decide by hook presence; fallback to Publication vs Report
            if (!string.IsNullOrWhiteSpace(hub.Hooks?.Article) && _handlers.TryGetValue(EntryType.Publication, out var art))
                return art;
            if (!string.IsNullOrWhiteSpace(hub.Hooks?.Document) && _handlers.TryGetValue(EntryType.Report, out var doc))
                return doc;
            if (!string.IsNullOrWhiteSpace(hub.Hooks?.LitSearch) && _handlers.TryGetValue(EntryType.Other, out var lit))
                return lit;
            return _handlers.Values.First();
        }

        private Task<CasResult> MoveToCasFromRelAsync(string rel, CancellationToken ct)
            => _content.MoveToCasAsync(_ws.GetAbsolutePath(rel), ct);

        // Fallback normalizers used only by the back-compat ctor
        private static string? FallbackNormalizeDoi(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (s.StartsWith("doi:", StringComparison.OrdinalIgnoreCase)) s = s[4..].Trim();
            var i = s.IndexOf("10.", StringComparison.OrdinalIgnoreCase);
            if (i >= 0) s = s[i..];
            s = s.TrimEnd('.', ',', ';', ')', ']', '}', '>', ':', '–', '—');
            return s.Contains('/') ? s.ToLowerInvariant() : null;
        }

        private static string? FallbackNormalizePmid(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (s.StartsWith("pmid:", StringComparison.OrdinalIgnoreCase)) s = s[5..].Trim();
            Span<char> buf = stackalloc char[s.Length];
            var j = 0;
            foreach (var ch in s)
                if (char.IsDigit(ch)) buf[j++] = ch;
            return j == 0 ? null : new string(buf[..j]);
        }
    }
}
