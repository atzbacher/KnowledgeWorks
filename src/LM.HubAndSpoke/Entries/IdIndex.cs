#nullable enable
using System;
using System.Collections.Concurrent;

namespace LM.HubSpoke.Entries
{
    /// <summary>
    /// Thread-safe DOI/PMID → entryId map using injected normalizers.
    /// </summary>
    internal sealed class IdIndex
    {
        private readonly ConcurrentDictionary<string, string> _doi = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _pmid = new(StringComparer.Ordinal);
        private readonly Func<string?, string?> _normDoi;
        private readonly Func<string?, string?> _normPmid;

        public IdIndex(Func<string?, string?> normDoi, Func<string?, string?> normPmid)
        {
            _normDoi = normDoi ?? throw new ArgumentNullException(nameof(normDoi));
            _normPmid = normPmid ?? throw new ArgumentNullException(nameof(normPmid));
        }

        public void AddOrUpdate(string? doi, string? pmid, string entryId)
        {
            var nd = _normDoi(doi);
            if (!string.IsNullOrWhiteSpace(nd)) _doi[nd!] = entryId;

            var np = _normPmid(pmid);
            if (!string.IsNullOrWhiteSpace(np)) _pmid[np!] = entryId;
        }

        public string? Find(string? doi, string? pmid)
        {
            var nd = _normDoi(doi);
            if (!string.IsNullOrWhiteSpace(nd) && _doi.TryGetValue(nd!, out var id1))
                return id1;

            var np = _normPmid(pmid);
            if (!string.IsNullOrWhiteSpace(np) && _pmid.TryGetValue(np!, out var id2))
                return id2;

            return null;
        }
    }
}
