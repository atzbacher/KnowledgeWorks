using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Persists PDF annotation overlays and preview images into the active workspace.
    /// </summary>
    public interface IPdfAnnotationPersistenceService
    {
        /// <summary>
        /// Writes the overlay JSON and preview images for a PDF entry, then records the update in the entry changelog.
        /// </summary>
        /// <param name="entryId">The workspace entry identifier backing the PDF.</param>
        /// <param name="pdfHash">The SHA-256 hash of the PDF file as produced by <see cref="IHasher"/>.</param>
        /// <param name="overlayJson">Serialized annotation overlay metadata.</param>
        /// <param name="previewImages">PNG preview images keyed by annotation identifier.</param>
        /// <param name="overlaySidecarRelativePath">Optional workspace-relative path for the overlay sidecar JSON. When omitted, a default path is chosen.</param>
        /// <param name="pdfRelativePath">Workspace-relative path to the PDF file backing the annotations.</param>
        /// <param name="annotations">Annotation metadata captured from the viewer bridge.</param>
        /// <param name="cancellationToken">Token used to observe cancellation.</param>
        Task PersistAsync(
            string entryId,
            string pdfHash,
            string overlayJson,
            IReadOnlyDictionary<string, byte[]> previewImages,
            string? overlaySidecarRelativePath,
            string? pdfRelativePath,
            IReadOnlyList<Models.Pdf.PdfAnnotationBridgeMetadata> annotations,
            CancellationToken cancellationToken);
    }
}
