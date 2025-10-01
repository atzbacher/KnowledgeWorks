using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.Pdf;
using LM.HubSpoke.Models;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Pdf;
using Xunit;

namespace LM.Infrastructure.Tests.Pdf
{
    public sealed class PdfAnnotationPersistenceServiceTests
    {
        [Fact]
        public async Task PersistAsync_WritesArtifactsToDefaultLocations()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var service = new PdfAnnotationPersistenceService(workspace);

            const string entryId = "entry-123";
            const string hash = "ABCDEF123456";
            var normalized = hash.ToLowerInvariant();
            const string overlayJson = "{\"hello\":\"world\"}";

            var previews = new Dictionary<string, byte[]>
            {
                ["ann1"] = new byte[] { 1, 2, 3 },
                ["ann2"] = new byte[] { 4, 5, 6 }
            };

            var pdfRelativePath = Path.Combine("library", "ab", "source.pdf");
            Directory.CreateDirectory(Path.Combine(temp.Path, "library", "ab"));

            var annotations = new List<PdfAnnotationBridgeMetadata>
            {
                new("ann1", "Snippet One", "Note One", "#FFFF98"),
                new("ann2", "Snippet Two", null, "#53FFBC")
            };

            await service.PersistAsync(entryId, hash, overlayJson, previews, null, pdfRelativePath, annotations, CancellationToken.None);

            var overlayPath = Path.Combine(temp.Path, "library", "ab", "source.overlay.json");
            Assert.True(File.Exists(overlayPath), $"Expected overlay at: {overlayPath}");
            Assert.Equal(overlayJson, await File.ReadAllTextAsync(overlayPath));

            foreach (var previewId in previews.Keys)
            {
                var previewPath = Path.Combine(temp.Path, "extraction", normalized, previewId + ".png");
                Assert.True(File.Exists(previewPath), $"Expected preview at: {previewPath}");
                Assert.Equal(previews[previewId], await File.ReadAllBytesAsync(previewPath));
            }

            var hookPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "pdf_annotations.json");
            Assert.True(File.Exists(hookPath));

            var hook = JsonSerializer.Deserialize<PdfAnnotationsHook>(await File.ReadAllTextAsync(hookPath));
            Assert.NotNull(hook);
            Assert.Equal("library/ab/source.overlay.json", hook!.OverlayPath);
            Assert.Equal(2, hook.Previews.Count);
            Assert.Contains(hook.Previews, p => p.AnnotationId == "ann1" && p.ImagePath == $"extraction/{normalized}/ann1.png");
            Assert.Contains(hook.Previews, p => p.AnnotationId == "ann2" && p.ImagePath == $"extraction/{normalized}/ann2.png");

            Assert.Equal(2, hook.Annotations.Count);
            var first = hook.Annotations.First(a => a.AnnotationId == "ann1");
            Assert.Equal("Snippet One", first.Text);
            Assert.Equal("Note One", first.Note);
            Assert.NotNull(first.Color);
            Assert.Equal(255, first.Color!.Red);
            Assert.Equal(255, first.Color.Green);
            Assert.Equal(152, first.Color.Blue);

            var second = hook.Annotations.First(a => a.AnnotationId == "ann2");
            Assert.Equal("Snippet Two", second.Text);
            Assert.Null(second.Note);
            Assert.NotNull(second.Color);
            Assert.Equal(83, second.Color!.Red);
            Assert.Equal(255, second.Color.Green);
            Assert.Equal(188, second.Color.Blue);

            var changeLogPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "changelog.json");
            var changeLog = JsonSerializer.Deserialize<EntryChangeLogHook>(await File.ReadAllTextAsync(changeLogPath));
            Assert.NotNull(changeLog);
            var changeEvent = Assert.Single(changeLog!.Events);
            Assert.Equal("pdf-annotations-updated", changeEvent.Action);
            Assert.Equal(Environment.UserName, changeEvent.PerformedBy);

            var debugOverlayPath = Path.Combine(temp.Path, "debug", normalized + ".debug.json");
            Assert.True(File.Exists(debugOverlayPath));
            Assert.Equal(overlayJson, await File.ReadAllTextAsync(debugOverlayPath));
        }

        [Fact]
        public async Task PersistAsync_UsesProvidedSidecarPath()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var service = new PdfAnnotationPersistenceService(workspace);

            const string entryId = "entry-456";
            const string hash = "A1B2C3D4";
            var normalized = hash.ToLowerInvariant();
            const string overlayJson = "{}";
            const string sidecar = "annotations/custom_overlay.json";

            var annotations = new List<PdfAnnotationBridgeMetadata>
            {
                new("ann", "Snippet", "Note")
            };

            await service.PersistAsync(entryId, hash, overlayJson, new Dictionary<string, byte[]>(), sidecar, Path.Combine("library", "ab", "source.pdf"), annotations, CancellationToken.None);

            var overlayPath = Path.Combine(temp.Path, sidecar);
            Assert.True(File.Exists(overlayPath));

            var hookPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "pdf_annotations.json");
            var hook = JsonSerializer.Deserialize<PdfAnnotationsHook>(await File.ReadAllTextAsync(hookPath));
            Assert.NotNull(hook);
            Assert.Equal(sidecar.Replace("\\", "/"), hook!.OverlayPath);
            var annotation = Assert.Single(hook.Annotations);
            Assert.Equal("Snippet", annotation.Text);
            Assert.Equal("Note", annotation.Note);
            Assert.Null(annotation.Color);

            var debugOverlayPath = Path.Combine(temp.Path, "debug", normalized + ".debug.json");
            Assert.True(File.Exists(debugOverlayPath));
            Assert.Equal(overlayJson, await File.ReadAllTextAsync(debugOverlayPath));
        }

        [Fact]
        public async Task PersistAsync_PreservesExistingPreviewsWhenNoNewImages()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var service = new PdfAnnotationPersistenceService(workspace);

            const string entryId = "entry-901";
            const string hash = "FACEBEEF";
            var normalized = hash.ToLowerInvariant();
            var pdfRelativePath = Path.Combine("library", "aa", "document.pdf");

            var firstAnnotations = new List<PdfAnnotationBridgeMetadata>
            {
                new("existing", "Initial", "First note", "#101010")
            };

            await service.PersistAsync(
                entryId,
                hash,
                "{\"existing\":true}",
                new Dictionary<string, byte[]>
                {
                    ["existing"] = new byte[] { 1, 1, 1 }
                },
                null,
                pdfRelativePath,
                firstAnnotations,
                CancellationToken.None);

            var updatedAnnotations = new List<PdfAnnotationBridgeMetadata>
            {
                new("existing", "Updated", "Second note", "#202020")
            };

            await service.PersistAsync(
                entryId,
                hash,
                "{\"updated\":true}",
                new Dictionary<string, byte[]>(),
                null,
                pdfRelativePath,
                updatedAnnotations,
                CancellationToken.None);

            var hookPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "pdf_annotations.json");
            var hook = JsonSerializer.Deserialize<PdfAnnotationsHook>(await File.ReadAllTextAsync(hookPath));
            Assert.NotNull(hook);
            var preview = Assert.Single(hook!.Previews);
            Assert.Equal("existing", preview.AnnotationId);
            Assert.Equal($"extraction/{normalized}/existing.png", preview.ImagePath);

            var annotation = Assert.Single(hook.Annotations);
            Assert.Equal("Updated", annotation.Text);
            Assert.Equal("Second note", annotation.Note);
            Assert.NotNull(annotation.Color);
            Assert.Equal(32, annotation.Color!.Red);
            Assert.Equal(32, annotation.Color.Green);
            Assert.Equal(32, annotation.Color.Blue);

            var previewPath = Path.Combine(temp.Path, "extraction", normalized, "existing.png");
            Assert.True(File.Exists(previewPath));
        }

        [Fact]
        public async Task PersistAsync_ThrowsWhenSidecarIsAbsolute()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var service = new PdfAnnotationPersistenceService(workspace);

            var absoluteSidecar = Path.Combine(temp.Path, "overlay.json");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.PersistAsync(
                    "entry-789",
                    "ABCD1234",
                    "{}",
                    new Dictionary<string, byte[]>(),
                    absoluteSidecar,
                    null,
                    Array.Empty<PdfAnnotationBridgeMetadata>(),
                    CancellationToken.None));
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_pdf_annotations_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
            }
        }
    }
}
