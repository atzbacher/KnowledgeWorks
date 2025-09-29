using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.Models;
using LM.Infrastructure.Hooks;

namespace LM.Infrastructure.Pdf
{
    /// <summary>
    /// Stores PDF annotation overlays and preview assets into the current workspace.
    /// </summary>
    public sealed class PdfAnnotationPersistenceService : IPdfAnnotationPersistenceService
    {
        private const string OverlayExtension = ".json";
        private const string PreviewExtension = ".png";

        private readonly IWorkSpaceService _workspace;
        private readonly HookWriter _hookWriter;

        public PdfAnnotationPersistenceService(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _hookWriter = new HookWriter(_workspace);
        }

        public async Task PersistAsync(
            string entryId,
            string pdfHash,
            string overlayJson,
            IReadOnlyDictionary<string, byte[]> previewImages,
            string? overlaySidecarRelativePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry identifier must be provided.", nameof(entryId));
            if (string.IsNullOrWhiteSpace(pdfHash))
                throw new ArgumentException("PDF hash must be provided.", nameof(pdfHash));
            var normalizedHash = pdfHash.Trim().ToLowerInvariant();
            if (normalizedHash.Length < 2)
                throw new ArgumentException("PDF hash must contain at least two characters.", nameof(pdfHash));
            if (overlayJson is null)
                throw new ArgumentNullException(nameof(overlayJson));

            var safePreviewImages = previewImages ?? new Dictionary<string, byte[]>(capacity: 0);

            cancellationToken.ThrowIfCancellationRequested();

            var overlayRelativePath = ResolveOverlayRelativePath(normalizedHash, overlaySidecarRelativePath);
            var overlayAbsolutePath = _workspace.GetAbsolutePath(overlayRelativePath);
            EnsureDirectoryForFile(overlayAbsolutePath);

            var overlayBytes = Encoding.UTF8.GetBytes(overlayJson);
            await File.WriteAllBytesAsync(overlayAbsolutePath, overlayBytes, cancellationToken).ConfigureAwait(false);

            var previewRootRelative = Path.Combine("extraction", normalizedHash);
            var previewRootAbsolute = _workspace.GetAbsolutePath(previewRootRelative);
            Directory.CreateDirectory(previewRootAbsolute);

            var previews = new List<PdfAnnotationPreview>(safePreviewImages.Count);

            foreach (var kvp in safePreviewImages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var annotationId = NormalizeAnnotationId(kvp.Key);
                if (annotationId is null || kvp.Value is null)
                {
                    continue;
                }

                var previewRelativePath = NormalizeRelativePath(Path.Combine(previewRootRelative, annotationId + PreviewExtension));
                var previewAbsolutePath = Path.Combine(previewRootAbsolute, annotationId + PreviewExtension);

                await File.WriteAllBytesAsync(previewAbsolutePath, kvp.Value, cancellationToken).ConfigureAwait(false);

                previews.Add(new PdfAnnotationPreview
                {
                    AnnotationId = annotationId,
                    ImagePath = previewRelativePath
                });
            }

            var hook = new PdfAnnotationsHook
            {
                OverlayPath = NormalizeRelativePath(overlayRelativePath),
                Previews = previews
            };

            await _hookWriter.SavePdfAnnotationsAsync(entryId, normalizedHash, hook, cancellationToken).ConfigureAwait(false);
        }

        private static string ResolveOverlayRelativePath(string pdfHash, string? sidecar)
        {
            if (!string.IsNullOrWhiteSpace(sidecar))
            {
                var trimmed = sidecar.Trim();
                if (Path.IsPathRooted(trimmed))
                {
                    throw new ArgumentException("Overlay sidecar path must be workspace-relative.", nameof(sidecar));
                }

                return NormalizeRelativePath(trimmed);
            }

            var folder = pdfHash[..2];
            var fileName = pdfHash + OverlayExtension;
            return NormalizeRelativePath(Path.Combine("library", folder, pdfHash, fileName));
        }

        private static string? NormalizeAnnotationId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var trimmed = raw.Trim();
            var sanitized = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        private static string NormalizeRelativePath(string path)
        {
            var interim = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return interim.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void EnsureDirectoryForFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
