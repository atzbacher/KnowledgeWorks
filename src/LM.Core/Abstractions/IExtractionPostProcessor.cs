using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Optional hook executed after a region export completes, enabling enrichment steps such as ML tagging.
    /// </summary>
    public interface IExtractionPostProcessor
    {
        /// <summary>
        /// Gets the display name for diagnostics and logging.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Determines whether the post-processor should run for the provided export result.
        /// </summary>
        bool CanHandle(RegionExportResult result);

        /// <summary>
        /// Performs the post-processing work. Implementations should honour the two-second guideline noted in the docs.
        /// </summary>
        Task PostProcessAsync(RegionExportResult result, CancellationToken cancellationToken = default);
    }
}
