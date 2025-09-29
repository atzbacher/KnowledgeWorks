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
            const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            var overlayRelative = $"library/{hash[..2]}/{hash}/{hash}.json";
            var overlayAbsolute = Path.Combine(temp.Path, overlayRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(overlayAbsolute)!);
            const string overlayPayload = "{\"foo\":\"bar\"}";
            await File.WriteAllTextAsync(overlayAbsolute, overlayPayload);

            var hookDirectory = Path.Combine(temp.Path, "entries", hash, "hooks");
            Directory.CreateDirectory(hookDirectory);
            var hook = new PdfAnnotationsHook
            {
                OverlayPath = overlayRelative
            };
            var hookPath = Path.Combine(hookDirectory, "pdf_annotations.json");
            await File.WriteAllTextAsync(hookPath, JsonSerializer.Serialize(hook, JsonStd.Options));

            var result = await reader.GetOverlayJsonAsync(hash, CancellationToken.None);

            Assert.Equal(overlayPayload, result);
        }

        [Fact]
        public async Task GetOverlayJsonAsync_ReturnsNullWhenHookMissing()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var reader = new PdfAnnotationOverlayReader(workspace);
            const string hash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            var result = await reader.GetOverlayJsonAsync(hash, CancellationToken.None);

            Assert.Null(result);
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
