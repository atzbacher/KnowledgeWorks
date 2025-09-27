#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Infrastructure.Metadata.EvidenceExtraction.Tables;
using Metadata = LM.Infrastructure.Tests.Metadata.EvidenceExtraction;
using UglyToad.PdfPig;
using Xunit;

namespace LM.Infrastructure.Tests.Metadata.EvidenceExtraction.Tables
{
    public sealed class TabulaSharpTableExtractorTests
    {
        [Fact]
        public async Task ExtractAsync_ReturnsStructuredTable()
        {
            using var temp = new TempDir();
            var pdfPath = Path.Combine(temp.Path, "table.pdf");
            Metadata.EvidenceExtraction.TestPdfBuilder.WriteSimpleBaselinePdf(pdfPath);

            using var document = PdfDocument.Open(pdfPath);
            var extractor = new TabulaSharpTableExtractor(new TabulaTableImageWriter());

            var tables = await extractor.ExtractAsync(document,
                                                      pdfPath,
                                                      Path.Combine(temp.Path, "tables"),
                                                      "hash",
                                                      CancellationToken.None);

            var table = Assert.Single(tables);
            Assert.Equal(3, table.Rows.Count);
            Assert.Equal("Group", table.Rows[0].Label, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Baseline Control", table.DetectedPopulations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Baseline Treatment", table.DetectedPopulations, StringComparer.OrdinalIgnoreCase);
            Assert.NotNull(table.ImageRelativePath);
            Assert.NotNull(table.CsvRelativePath);
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw-tabula-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
