#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using LM.Core.Models.DataExtraction;
using LM.Infrastructure.Metadata.EvidenceExtraction;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Utils;
using Xunit;

namespace LM.Infrastructure.Tests.Metadata.EvidenceExtraction
{
    public sealed class DataExtractionPreprocessorTests
    {
        [Fact]
        public async Task PreprocessAsync_ProducesTablesFiguresAndProvenance()
        {
            using var temp = new TempDir();
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var pdfPath = Path.Combine(temp.Path, "sample.pdf");
            TestPdfBuilder.WriteSimpleBaselinePdf(pdfPath);

            var hasher = new HashingService();
            var preprocessor = new DataExtractionPreprocessor(hasher, workspace);

            var request = new DataExtractionPreprocessRequest(pdfPath);
            var result = await preprocessor.PreprocessAsync(request);

            Assert.NotNull(result);
            Assert.False(result.IsEmpty);
            Assert.NotEmpty(result.Tables);
            Assert.NotEmpty(result.Provenance.SourceSha256);
            Assert.Equal(Path.GetFileName(pdfPath), result.Provenance.SourceFileName);

            var table = Assert.Single(result.Tables);
            Assert.Contains("Baseline Control", table.DetectedPopulations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Baseline Treatment", table.DetectedPopulations, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(TableClassificationKind.Baseline, table.Classification);
            Assert.False(string.IsNullOrWhiteSpace(table.FriendlyName));
            Assert.NotEmpty(table.Tags);
            Assert.NotEmpty(table.Regions);
            Assert.NotEmpty(table.PageLocations);
            Assert.False(string.IsNullOrWhiteSpace(table.ImageRelativePath));
            Assert.False(string.IsNullOrWhiteSpace(table.ImageProvenanceHash));

            var absoluteTable = workspace.GetAbsolutePath(table.CsvRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(absoluteTable));
            var absoluteImage = workspace.GetAbsolutePath(table.ImageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(absoluteImage));
            foreach (var region in table.Regions)
            {
                Assert.InRange(region.X, 0d, 1d);
                Assert.InRange(region.Y, 0d, 1d);
                Assert.InRange(region.Width, 0d, 1d);
                Assert.InRange(region.Height, 0d, 1d);
            }

            Assert.NotEmpty(result.Figures);
            var figure = Assert.Single(result.Figures);
            var figurePath = workspace.GetAbsolutePath(figure.ThumbnailRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(figurePath));
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw-preprocessor-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                        Directory.Delete(Path, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
