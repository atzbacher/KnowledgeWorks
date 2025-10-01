using System;
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
    public sealed class PdfAnnotationOverlayReaderTests
    {
        [Fact]
        public async Task GetOverlayJsonAsync_ReturnsOverlayContent()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var reader = new PdfAnnotationOverlayReader(workspace);
            const string entryId = "entry-7";
            const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            var overlayRelative = $"library/{hash[..2]}/{hash}/{hash}.json";
            var overlayAbsolute = Path.Combine(temp.Path, overlayRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(overlayAbsolute)!);
            const string overlayPayload = "{\"foo\":\"bar\"}";
            await File.WriteAllTextAsync(overlayAbsolute, overlayPayload);

            var hookDirectory = Path.Combine(temp.Path, "entries", entryId, "hooks");
            Directory.CreateDirectory(hookDirectory);
            var hook = new PdfAnnotationsHook
            {
                OverlayPath = overlayRelative
            };
            var hookPath = Path.Combine(hookDirectory, "pdf_annotations.json");
            await File.WriteAllTextAsync(hookPath, JsonSerializer.Serialize(hook, JsonStd.Options));

            var result = await reader.GetOverlayJsonAsync(entryId, hash, CancellationToken.None);

            Assert.Equal(overlayPayload, result);
            Assert.False(Directory.Exists(Path.Combine(temp.Path, "entries", hash)), "Legacy hash directory should not be created.");
        }

        [Fact]
        public async Task GetOverlayJsonAsync_ReturnsNullWhenHookMissing()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var reader = new PdfAnnotationOverlayReader(workspace);
            const string entryId = "entry-9";
            const string hash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            var result = await reader.GetOverlayJsonAsync(entryId, hash, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetOverlayJsonAsync_FallsBackToLegacyHashDirectory()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var reader = new PdfAnnotationOverlayReader(workspace);
            const string entryId = "entry-legacy";
            const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            var overlayRelative = $"library/{hash[..2]}/{hash}/{hash}.json";
            var overlayAbsolute = Path.Combine(temp.Path, overlayRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(overlayAbsolute)!);
            const string payload = "{\"legacy\":true}";
            await File.WriteAllTextAsync(overlayAbsolute, payload);

            var legacyHookDir = Path.Combine(temp.Path, "entries", hash, "hooks");
            Directory.CreateDirectory(legacyHookDir);
            var legacyHookPath = Path.Combine(legacyHookDir, "pdf_annotations.json");
            await File.WriteAllTextAsync(legacyHookPath, JsonSerializer.Serialize(new PdfAnnotationsHook
            {
                OverlayPath = overlayRelative
            }, JsonStd.Options));

            var result = await reader.GetOverlayJsonAsync(entryId, hash, CancellationToken.None);

            Assert.Equal(payload, result);
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "overlay_reader_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { }
            }
        }
    }
}
