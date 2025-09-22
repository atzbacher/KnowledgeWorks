using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Produces lightweight previews for selections so the UI can render thumbnails and hover cards quickly.
    /// Implementations may cache previews on disk but must honour cancellation and offline quotas.
    /// </summary>
    public interface IRegionPreviewService
    {
        /// <summary>
        /// Generates or retrieves a preview for the given export request.
        /// </summary>
        Task<RegionPreview> RenderAsync(RegionExportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cached previews associated with the supplied cache key or region hash.
        /// </summary>
        Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default);
    }
}
