using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Persists and queries <see cref="RegionDescriptor"/> records backed by the extractor SQLite store.
    /// Implementations provide history lookups for the UI and Office add-in integration.
    /// </summary>
    public interface IExtractionRepository
    {
        /// <summary>
        /// Inserts or updates a descriptor along with its FTS index entries.
        /// </summary>
        Task UpsertAsync(RegionDescriptor descriptor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches a descriptor by its region hash.
        /// </summary>
        Task<RegionDescriptor?> GetAsync(string regionHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerates descriptors for a specific entry hub ordered by newest first.
        /// </summary>
        IAsyncEnumerable<RegionDescriptor> ListByEntryAsync(string entryHubId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the most recent descriptors across the workspace (limited by <paramref name="take"/>).
        /// </summary>
        Task<IReadOnlyList<RegionDescriptor>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a recent extraction session so UIs can display run history.
        /// </summary>
        Task SaveSessionAsync(RegionExportResult result, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the most recent extraction sessions, including exporter metadata.
        /// </summary>
        Task<IReadOnlyList<RegionExportResult>> GetRecentSessionsAsync(int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches descriptors via the OCR full-text index.
        /// </summary>
        Task<IReadOnlyList<RegionDescriptor>> SearchAsync(string query, int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the last known export status for a region.
        /// </summary>
        Task UpdateStatusAsync(string regionHash, RegionExportStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a descriptor and its assets from the repository.
        /// </summary>
        Task DeleteAsync(string regionHash, CancellationToken cancellationToken = default);
    }
}
