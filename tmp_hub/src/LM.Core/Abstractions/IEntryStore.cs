using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;
using LM.Core.Models.Filters;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Source-of-truth store: each Entry is a JSON file under the workspace.
    /// </summary>
    public interface IEntryStore
    {
        /// <summary>Scan the workspace entries folder and build in-memory indices.</summary>
        Task InitializeAsync(CancellationToken ct = default);

        /// <summary>Persist an entry to its JSON file (creates folder if needed). Updates in-memory indices.</summary>
        Task SaveAsync(Entry entry, CancellationToken ct = default);

        /// <summary>Get an entry by id (null if not found).</summary>
        Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default);

        /// <summary>Return all entries (from in-memory cache built at Initialize).</summary>
        IAsyncEnumerable<Entry> EnumerateAsync(CancellationToken ct = default);

        /// <summary>Metadata-only fielded search over the loaded entries.</summary>
        Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default);

        /// <summary>Find an entry whose main-file hash matches.</summary>
        Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default);

        /// <summary>Find possible matches by title (contains) and approximate year proximity (±1).</summary>
        Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default);

        // NEW: Fast DOI/PMID lookup (removes O(N) scans in staging)
        Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default);
    }
}
