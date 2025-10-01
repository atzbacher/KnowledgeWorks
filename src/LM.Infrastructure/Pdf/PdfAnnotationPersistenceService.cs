using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models.Pdf;
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
        private const string DebugDirectoryName = "debug";

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
            string? pdfRelativePath,
            IReadOnlyList<PdfAnnotationBridgeMetadata> annotations,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry identifier must be provided.", nameof(entryId));
            if (string.IsNullOrWhiteSpace(pdfHash))
                throw new ArgumentException("PDF hash must be provided.", nameof(pdfHash));
            var normalizedEntryId = entryId.Trim();
            if (normalizedEntryId.Length == 0)
                throw new ArgumentException("Entry identifier must be provided.", nameof(entryId));

            var normalizedHash = pdfHash.Trim().ToLowerInvariant();
            if (normalizedHash.Length < 4)
                throw new ArgumentException("PDF hash must contain at least four characters.", nameof(pdfHash));
            if (overlayJson is null)
                throw new ArgumentNullException(nameof(overlayJson));

            var safePreviewImages = previewImages ?? new Dictionary<string, byte[]>(capacity: 0);
            var annotationSnapshot = annotations ?? Array.Empty<PdfAnnotationBridgeMetadata>();

            cancellationToken.ThrowIfCancellationRequested();

            var overlayRelativePath = ResolveOverlayRelativePath(normalizedHash, overlaySidecarRelativePath, pdfRelativePath);
            var overlayAbsolutePath = _workspace.GetAbsolutePath(overlayRelativePath);
            EnsureDirectoryForFile(overlayAbsolutePath);

            var overlayBytes = Encoding.UTF8.GetBytes(overlayJson);
            await File.WriteAllBytesAsync(overlayAbsolutePath, overlayBytes, cancellationToken).ConfigureAwait(false);

            var previewRootRelative = Path.Combine("extraction", normalizedHash);
            var previewRootAbsolute = _workspace.GetAbsolutePath(previewRootRelative);
            Directory.CreateDirectory(previewRootAbsolute);

            var previewMap = await LoadExistingPreviewsAsync(normalizedEntryId, cancellationToken).ConfigureAwait(false);

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

                previewMap[annotationId] = previewRelativePath;
            }

            var previews = new List<PdfAnnotationPreview>(previewMap.Count);
            foreach (var kvp in previewMap)
            {
                previews.Add(new PdfAnnotationPreview
                {
                    AnnotationId = kvp.Key,
                    ImagePath = kvp.Value
                });
            }

            var hook = new PdfAnnotationsHook
            {
                OverlayPath = NormalizeRelativePath(overlayRelativePath),
                Previews = previews,
                Annotations = BuildAnnotationMetadata(annotationSnapshot)
            };

            await _hookWriter.SavePdfAnnotationsAsync(normalizedEntryId, normalizedHash, hook, cancellationToken).ConfigureAwait(false);

            await WriteDebugOverlaySnapshotAsync(normalizedHash, overlayJson, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Dictionary<string, string>> LoadExistingPreviewsAsync(string entryId, CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var hookRelative = Path.Combine("entries", entryId, "hooks", "pdf_annotations.json");
            var hookAbsolute = _workspace.GetAbsolutePath(hookRelative);

            if (string.IsNullOrWhiteSpace(hookAbsolute) || !File.Exists(hookAbsolute))
            {
                return result;
            }

            try
            {
                await using var stream = new FileStream(
                    hookAbsolute,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                var hook = await JsonSerializer.DeserializeAsync<PdfAnnotationsHook>(stream, JsonStd.Options, cancellationToken).ConfigureAwait(false);
                if (hook?.Previews is null || hook.Previews.Count == 0)
                {
                    return result;
                }

                foreach (var preview in hook.Previews)
                {
                    if (preview is null)
                    {
                        continue;
                    }

                    var id = NormalizeAnnotationId(preview.AnnotationId);
                    if (id is null || string.IsNullOrWhiteSpace(preview.ImagePath))
                    {
                        continue;
                    }

                    var normalizedPath = NormalizeRelativePath(preview.ImagePath);
                    var absolutePath = _workspace.GetAbsolutePath(normalizedPath);
                    if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                    {
                        continue;
                    }

                    result[id] = normalizedPath;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ignore malformed hooks and fall back to regenerated previews.
            }

            return result;
        }

        private async Task WriteDebugOverlaySnapshotAsync(string pdfHash, string overlayJson, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(overlayJson) || string.IsNullOrWhiteSpace(pdfHash))
            {
                return;
            }

            try
            {
                var debugRelative = Path.Combine(DebugDirectoryName, pdfHash + ".debug.json");
                var debugAbsolute = _workspace.GetAbsolutePath(debugRelative);
                EnsureDirectoryForFile(debugAbsolute);

                await File.WriteAllTextAsync(debugAbsolute, overlayJson, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Debug copy is best-effort. Ignore failures.
            }
        }

        private static string ResolveOverlayRelativePath(string pdfHash, string? sidecar, string? pdfRelativePath)
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

            if (!string.IsNullOrWhiteSpace(pdfRelativePath))
            {
                var sanitized = NormalizeRelativePath(pdfRelativePath.Trim());
                var directory = Path.GetDirectoryName(sanitized);
                var fileName = Path.GetFileNameWithoutExtension(sanitized);

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var overlayFileName = fileName + ".overlay.json";
                    var combined = string.IsNullOrWhiteSpace(directory)
                        ? overlayFileName
                        : Path.Combine(directory, overlayFileName);
                    return NormalizeRelativePath(combined);
                }
            }

            var firstSegment = pdfHash[..2];
            var secondSegment = pdfHash[2..4];
            var hashedOverlayFileName = pdfHash + ".overlay" + OverlayExtension;
            return NormalizeRelativePath(Path.Combine("library", firstSegment, secondSegment, hashedOverlayFileName));
        }

        private static List<PdfAnnotationMetadata> BuildAnnotationMetadata(IReadOnlyList<PdfAnnotationBridgeMetadata> annotations)
        {
            if (annotations is null || annotations.Count == 0)
            {
                return new List<PdfAnnotationMetadata>();
            }

            var map = new Dictionary<string, PdfAnnotationMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var annotation in annotations)
            {
                if (annotation is null || string.IsNullOrWhiteSpace(annotation.AnnotationId))
                {
                    continue;
                }

                var normalizedId = annotation.AnnotationId.Trim();
                map[normalizedId] = new PdfAnnotationMetadata
                {
                    AnnotationId = normalizedId,
                    Text = annotation.Text,
                    Note = annotation.Note
                };
            }

            return new List<PdfAnnotationMetadata>(map.Values);

        }

        private static List<PdfAnnotationMetadata> BuildAnnotationMetadata(IReadOnlyList<PdfAnnotationBridgeMetadata> annotations)
        {
            if (annotations is null || annotations.Count == 0)
            {
                return new List<PdfAnnotationMetadata>();
            }

            var map = new Dictionary<string, PdfAnnotationMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var annotation in annotations)
            {
                if (annotation is null || string.IsNullOrWhiteSpace(annotation.AnnotationId))
                {
                    continue;
                }

                var normalizedId = annotation.AnnotationId.Trim();
                map[normalizedId] = new PdfAnnotationMetadata
                {
                    AnnotationId = normalizedId,
                    Text = annotation.Text,
                    Note = annotation.Note
                };
            }

            return new List<PdfAnnotationMetadata>(map.Values);
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
