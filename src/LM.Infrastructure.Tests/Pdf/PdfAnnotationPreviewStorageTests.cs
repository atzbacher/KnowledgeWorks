using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LM.Infrastructure.Pdf;
using Xunit;

namespace LM.Infrastructure.Tests.Pdf
{
    public sealed class PdfAnnotationPreviewStorageTests : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly TestWorkSpaceService _workspace;

        public PdfAnnotationPreviewStorageTests()
        {
            _workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workspaceRoot);
            _workspace = new TestWorkSpaceService(_workspaceRoot);
        }

        [Fact]
        public async Task SaveAsync_WritesPreviewToWorkspace()
        {
            var storage = new PdfAnnotationPreviewStorage(_workspace);
            var pngBytes = new byte[] { 137, 80, 78, 71 };

            var relativePath = await storage.SaveAsync("ABCD1234", "my-annotation", pngBytes, CancellationToken.None);

            relativePath.Should().Be("extraction/ABCD1234/my-annotation.png");
            var absolutePath = Path.Combine(_workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(absolutePath).Should().BeTrue();
            File.ReadAllBytes(absolutePath).Should().Equal(pngBytes);
        }

        [Fact]
        public async Task SaveAsync_NormalizesAnnotationId()
        {
            var storage = new PdfAnnotationPreviewStorage(_workspace);
            var pngBytes = new byte[] { 1, 2, 3 };

            var relativePath = await storage.SaveAsync("DE", "../unsafe\\id", pngBytes, CancellationToken.None);

            relativePath.Should().Be("extraction/DE/id.png");
        }

        public void Dispose()
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
    }
}
