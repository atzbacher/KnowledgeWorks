using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;

namespace LM.Infrastructure.Entries
{
    /// <summary>
    /// Persists entries as JSON files under: {workspace}\entries\{id}\entry.json
    /// Maintains an in-memory cache for fast fielded search.
    /// </summary>
    public sealed class JsonEntryStore : IEntryStore
    {
        private readonly IWorkSpaceService _ws;
        private readonly JsonSerializerOptions _json;
        private readonly IDoiNormalizer _doi;
        private readonly IPmidNormalizer _pmid;
        private readonly Dictionary<string, string> _byDoi = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _byPmid = new(StringComparer.Ordinal);
      
        private readonly Dictionary<string, Entry> _byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _byHash = new(StringComparer.OrdinalIgnoreCase);
        private string _entriesRoot = "";

        public JsonEntryStore(IWorkSpaceService ws, IDoiNormalizer? doiNormalizer = null, IPmidNormalizer? pmidNormalizer = null)
        {
            _ws = ws ?? throw new ArgumentNullException(nameof(ws));
            _doi = doiNormalizer ?? new LM.Infrastructure.Text.DoiNormalizer();
            _pmid = pmidNormalizer ?? new LM.Infrastructure.Text.PmidNormalizer();

            _json = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }


        public async Task InitializeAsync(CancellationToken ct = default)
        {
            _entriesRoot = Path.Combine(_ws.GetWorkspaceRoot(), "entries");
            Directory.CreateDirectory(_entriesRoot);

            _byId.Clear();
            _byHash.Clear();
            _byDoi.Clear();
            _byPmid.Clear();


            // Load all entries/*.*/entry.json (flat and nested)
            foreach (var entryJson in Directory.EnumerateFiles(_entriesRoot, "entry.json", SearchOption.AllDirectories))
            {
                try
                {
                    await using var fs = new FileStream(entryJson, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var e = await JsonSerializer.DeserializeAsync<Entry>(fs, _json, ct);
                    if (e is null || string.IsNullOrWhiteSpace(e.Id)) continue;

                    _byId[e.Id] = e;
                    if (!string.IsNullOrWhiteSpace(e.MainFileHashSha256))
                        _byHash[e.MainFileHashSha256] = e.Id;

                    if (!string.IsNullOrWhiteSpace(e.Doi))
                    {
                        var nd = _doi.Normalize(e.Doi);
                        if (!string.IsNullOrWhiteSpace(nd)) _byDoi[nd!] = e.Id;
                    }
                    if (!string.IsNullOrWhiteSpace(e.Pmid))
                    {
                        var np = _pmid.Normalize(e.Pmid);
                        if (!string.IsNullOrWhiteSpace(np)) _byPmid[np!] = e.Id;
                    }
                }
                catch
                {
                    // Ignore broken JSON files for now; we can surface diagnostics later.
                }
            }
        }

        public async Task SaveAsync(Entry entry, CancellationToken ct = default)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.Id)) entry.Id = Guid.NewGuid().ToString("N");

            var dir = Path.Combine(_entriesRoot, entry.Id);
            Directory.CreateDirectory(dir);

            var jsonPath = Path.Combine(dir, "entry.json");
            var lockPath = jsonPath + ".lock";

            // simple lock file: fail if exists and fresh (<5 minutes); otherwise overwrite
            FileStream? lockFs = null;
            try
            {
                lockFs = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using (lockFs) { /* just owning the handle is enough */ }

                var tmp = jsonPath + ".tmp";
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(fs, entry, _json, ct);
                    await fs.FlushAsync(ct);
                }
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                File.Move(tmp, jsonPath);

                // Update cache
                _byId[entry.Id] = entry;
                if (!string.IsNullOrWhiteSpace(entry.MainFileHashSha256))
                    _byHash[entry.MainFileHashSha256] = entry.Id;
                if (!string.IsNullOrWhiteSpace(entry.Doi))
                {
                    var nd = _doi.Normalize(entry.Doi);
                    if (!string.IsNullOrWhiteSpace(nd)) _byDoi[nd!] = entry.Id;
                }
                if (!string.IsNullOrWhiteSpace(entry.Pmid))
                {
                    var np = _pmid.Normalize(entry.Pmid);
                    if (!string.IsNullOrWhiteSpace(np)) _byPmid[np!] = entry.Id;
                }

            }
            finally
            {
                try { File.Delete(lockPath); } catch { /* ignore */ }
            }
        }

        public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            _byId.TryGetValue(id, out var e);
            return Task.FromResult<Entry?>(e);
        }

        public async IAsyncEnumerable<Entry> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var e in _byId.Values)
            {
                ct.ThrowIfCancellationRequested();
                yield return e;
                await Task.Yield();
            }
        }

        public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(doi))
            {
                var nd = _doi.Normalize(doi);
                if (!string.IsNullOrWhiteSpace(nd) && _byDoi.TryGetValue(nd!, out var id1) && _byId.TryGetValue(id1, out var e1))
                    return Task.FromResult<Entry?>(e1);
            }
            if (!string.IsNullOrWhiteSpace(pmid))
            {
                var np = _pmid.Normalize(pmid);
                if (!string.IsNullOrWhiteSpace(np) && _byPmid.TryGetValue(np!, out var id2) && _byId.TryGetValue(id2, out var e2))
                    return Task.FromResult<Entry?>(e2);
            }
            return Task.FromResult<Entry?>(null);
        }



        public Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter f, CancellationToken ct = default)
        {
            IEnumerable<Entry> q = _byId.Values;

            if (!string.IsNullOrWhiteSpace(f.TitleContains))
            {
                var needle = f.TitleContains.Trim();
                q = q.Where(e => e.Title?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (!string.IsNullOrWhiteSpace(f.AuthorContains))
            {
                var needle = f.AuthorContains.Trim();
                q = q.Where(e => (e.Authors?.Any(a => a.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ?? false));
            }
            if (f.TypesAny is { Length: > 0 })
            {
                var set = new HashSet<EntryType>(f.TypesAny);
                q = q.Where(e => set.Contains(e.Type));
            }
            if (f.YearFrom.HasValue) q = q.Where(e => !e.Year.HasValue || e.Year >= f.YearFrom.Value);
            if (f.YearTo.HasValue)   q = q.Where(e => !e.Year.HasValue || e.Year <= f.YearTo.Value);

            if (f.IsInternal.HasValue)
            {
                var flag = f.IsInternal.Value;
                q = q.Where(e => e.IsInternal == flag);
            }
            if (f.TagsAny.Count > 0)
            {
                var tags = new HashSet<string>(f.TagsAny, StringComparer.OrdinalIgnoreCase);
                q = q.Where(e => e.Tags?.Any(t => tags.Contains(t)) ?? false);
            }

            var list = q.OrderByDescending(e => e.Year.HasValue).ThenBy(e => e.Title).Take(1000).ToList();
            return Task.FromResult<IReadOnlyList<Entry>>(list);
        }

        public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
        {
            if (_byHash.TryGetValue(sha256, out var id) && _byId.TryGetValue(id, out var e))
                return Task.FromResult<Entry?>(e);
            return Task.FromResult<Entry?>(null);
        }

        public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
        {
            IEnumerable<Entry> q = _byId.Values;
            if (!string.IsNullOrWhiteSpace(title))
            {
                var needle = title.Trim();
                q = q.Where(e => e.Title?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (year.HasValue)
            {
                int y = year.Value;
                q = q.Where(e => !e.Year.HasValue || Math.Abs(e.Year.Value - y) <= 1);
            }
            return Task.FromResult<IReadOnlyList<Entry>>(q.Take(50).ToList());
        }


    }
}
