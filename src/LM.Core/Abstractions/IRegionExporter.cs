using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Performs region exports by writing cropped assets, OCR text, and metadata described in the visual extractor docs.
    /// Implementations must be cancellable and avoid blocking the UI thread.
    /// </summary>
    public interface IRegionExporter
    {
        /// <summary>
        /// Gets the unique identifier for the exporter (e.g., <c>image/png</c>, <c>office/ppmx</c>).
        /// </summary>
        string ExporterId { get; }

        /// <summary>
        /// Determines whether the exporter can handle the supplied request.
        /// </summary>
        bool CanHandle(RegionExportRequest request);

        /// <summary>
        /// Executes the export and returns the generated assets and descriptor updates.
        /// </summary>
        Task<RegionExportResult> ExportAsync(RegionExportRequest request, CancellationToken cancellationToken = default);
    }
}
