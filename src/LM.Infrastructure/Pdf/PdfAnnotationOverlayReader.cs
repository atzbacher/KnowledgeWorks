using System;
using System.Diagnostics;
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
        private readonly IEntryStore _entryStore;

        public PdfAnnotationOverlayReader(IWorkSpaceService workspace, IEntryStore entryStore)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _entryStore = entryStore ?? throw new ArgumentNullException(nameof(entryStore));
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

            Trace.WriteLine($"[PdfAnnotationOverlayReader] Resolving overlay for entry '{entryId}' and hash '{pdfHash}'.");

            var entry = await _entryStore.FindByHashAsync(pdfHash, cancellationToken).ConfigureAwait(false);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                Trace.WriteLine($"[PdfAnnotationOverlayReader] No entry found for hash '{pdfHash}'.");
                return null;
            }

            if (!string.Equals(entry.Id, entryId, StringComparison.Ordinal))
            {
                Trace.WriteLine($"[PdfAnnotationOverlayReader] Provided entry id '{entryId}' differs from resolved '{entry.Id}'. Using resolved id.");
            }

            var hookRelativePath = Path.Combine("entries", entry.Id, "hooks", "pdf_annotations.json");
            var hookAbsolutePath = _workspace.GetAbsolutePath(hookRelativePath);

            if (string.IsNullOrWhiteSpace(hookAbsolutePath) || !File.Exists(hookAbsolutePath))
            {
                Trace.WriteLine($"[PdfAnnotationOverlayReader] Hook file missing for entry '{entry.Id}'.");
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
                    Trace.WriteLine($"[PdfAnnotationOverlayReader] Hook payload missing overlay path for entry '{entry.Id}'.");
                    return null;
                }

                var overlayRelative = hook.OverlayPath.Trim();
                var overlayAbsolute = _workspace.GetAbsolutePath(overlayRelative);
                if (string.IsNullOrWhiteSpace(overlayAbsolute) || !File.Exists(overlayAbsolute))
                {
                    Trace.WriteLine($"[PdfAnnotationOverlayReader] Overlay file '{overlayRelative}' missing for entry '{entry.Id}'.");
                    return null;
                }

                Trace.WriteLine($"[PdfAnnotationOverlayReader] Overlay resolved for entry '{entry.Id}'.");
                return await File.ReadAllTextAsync(overlayAbsolute, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                Trace.WriteLine($"[PdfAnnotationOverlayReader] Failed to deserialize overlay hook for entry '{entry.Id}'.");
                return null;
            }
        }
    }
}
