using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Pdf
{
    public sealed class PdfAnnotationOverlayReader : IPdfAnnotationOverlayReader
    {
        private readonly IWorkSpaceService _workspace;

        public PdfAnnotationOverlayReader(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<string?> GetOverlayJsonAsync(string entryId, string pdfHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry identifier must be provided.", nameof(entryId));
            }
            if (string.IsNullOrWhiteSpace(pdfHash))
            {
                throw new ArgumentException("PDF hash must be provided.", nameof(pdfHash));
            }

            var normalizedEntryId = entryId.Trim();
            if (normalizedEntryId.Length == 0)
            {
                throw new ArgumentException("Entry identifier must be provided.", nameof(entryId));
            }
            var normalizedHash = pdfHash.Trim().ToLowerInvariant();
            var hookRelativePath = Path.Combine("entries", normalizedHash, "hooks", "pdf_annotations.json");
            var hookAbsolutePath = _workspace.GetAbsolutePath(Path.Combine("entries", normalizedEntryId, "hooks", "pdf_annotations.json"));

            if (string.IsNullOrWhiteSpace(hookAbsolutePath) || !File.Exists(hookAbsolutePath))
            {
                hookAbsolutePath = _workspace.GetAbsolutePath(hookRelativePath);
            }

            if (string.IsNullOrWhiteSpace(hookAbsolutePath) || !File.Exists(hookAbsolutePath))
            {
                return null;
            }

            try
            {
                await using var hookStream = new FileStream(
                    hookAbsolutePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                var hook = await JsonSerializer.DeserializeAsync<HookM.PdfAnnotationsHook>(
                    hookStream,
                    HookM.JsonStd.Options,
                    cancellationToken).ConfigureAwait(false);

                if (hook is null || string.IsNullOrWhiteSpace(hook.OverlayPath))
                {
                    return null;
                }

                var overlayRelative = hook.OverlayPath.Trim();
                var overlayAbsolute = _workspace.GetAbsolutePath(overlayRelative);
                if (string.IsNullOrWhiteSpace(overlayAbsolute) || !File.Exists(overlayAbsolute))
                {
                    return null;
                }

                return await File.ReadAllTextAsync(overlayAbsolute, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }
    }
}
