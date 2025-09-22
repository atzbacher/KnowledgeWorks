using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Coordinates region export requests, dispatching work to exporters and surfacing completion/failure events to the UI layer.
    /// </summary>
    public interface IVisualExtractionCoordinator
    {
        /// <summary>
        /// Raised when an export completes successfully.
        /// </summary>
        event EventHandler<RegionExportCompleted>? ExportCompleted;

        /// <summary>
        /// Raised when an export fails.
        /// </summary>
        event EventHandler<RegionExportFailed>? ExportFailed;

        /// <summary>
        /// Queues a region export and returns the result once finished.
        /// Implementations may offload work to background jobs but should still honour cancellation.
        /// </summary>
        Task<RegionExportResult> QueueExportAsync(RegionExportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to cancel a queued or running export identified by region hash.
        /// </summary>
        Task CancelAsync(string regionHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches a descriptor for a previously exported region.
        /// </summary>
        Task<RegionDescriptor?> GetDescriptorAsync(string regionHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerates recent exports for display in history panes.
        /// </summary>
        IAsyncEnumerable<RegionDescriptor> EnumerateRecentAsync(int take, CancellationToken cancellationToken = default);
    }
}
