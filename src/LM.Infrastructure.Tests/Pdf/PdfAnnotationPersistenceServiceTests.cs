using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
            const string overlayJson = "{\"hello\":\"world\"}";

            var previews = new Dictionary<string, byte[]>
            {
                ["ann1"] = new byte[] { 1, 2, 3 },
                ["ann2"] = new byte[] { 4, 5, 6 }
            };

            await service.PersistAsync(entryId, hash, overlayJson, previews, null, CancellationToken.None);

            var overlayPath = Path.Combine(temp.Path, "library", hash[..2], hash, hash + ".json");
            Assert.True(File.Exists(overlayPath), $"Expected overlay at: {overlayPath}");
            Assert.Equal(overlayJson, await File.ReadAllTextAsync(overlayPath));

            foreach (var previewId in previews.Keys)
            {
                var previewPath = Path.Combine(temp.Path, "extraction", hash, previewId + ".png");
                Assert.True(File.Exists(previewPath), $"Expected preview at: {previewPath}");
                Assert.Equal(previews[previewId], await File.ReadAllBytesAsync(previewPath));
            }

            var hookPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "pdf_annotations.json");
            Assert.True(File.Exists(hookPath));

            var hook = JsonSerializer.Deserialize<PdfAnnotationsHook>(await File.ReadAllTextAsync(hookPath));
            Assert.NotNull(hook);
            Assert.Equal($"library/{hash[..2]}/{hash}/{hash}.json", hook!.OverlayPath);
            Assert.Equal(2, hook.Previews.Count);
            Assert.Contains(hook.Previews, p => p.AnnotationId == "ann1" && p.ImagePath == $"extraction/{hash}/ann1.png");
            Assert.Contains(hook.Previews, p => p.AnnotationId == "ann2" && p.ImagePath == $"extraction/{hash}/ann2.png");

            var changeLogPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "changelog.json");
            var changeLog = JsonSerializer.Deserialize<EntryChangeLogHook>(await File.ReadAllTextAsync(changeLogPath));
            Assert.NotNull(changeLog);
            var changeEvent = Assert.Single(changeLog!.Events);
            Assert.Equal("pdf-annotations-updated", changeEvent.Action);
            Assert.Equal(Environment.UserName, changeEvent.PerformedBy);
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
            const string overlayJson = "{}";
            const string sidecar = "annotations/custom_overlay.json";

            await service.PersistAsync(entryId, hash, overlayJson, new Dictionary<string, byte[]>(), sidecar, CancellationToken.None);

            var overlayPath = Path.Combine(temp.Path, sidecar);
            Assert.True(File.Exists(overlayPath));

            var hookPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "pdf_annotations.json");
            var hook = JsonSerializer.Deserialize<PdfAnnotationsHook>(await File.ReadAllTextAsync(hookPath));
            Assert.NotNull(hook);
            Assert.Equal(sidecar.Replace('\', '/'), hook!.OverlayPath);
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
