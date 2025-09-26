#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
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
            CreateSimplePdf(pdfPath);

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

            var absoluteTable = workspace.GetAbsolutePath(table.CsvRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(absoluteTable));

            Assert.NotEmpty(result.Figures);
            var figure = Assert.Single(result.Figures);
            var figurePath = workspace.GetAbsolutePath(figure.ThumbnailRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(figurePath));
        }

        private static void CreateSimplePdf(string path)
        {
            var header = "%PDF-1.4\n";
            var objects = new[]
            {
                "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
                "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
                "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n",
                BuildContentObject(),
                "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"
            };

            var builder = new StringBuilder();
            builder.Append(header);
            var offsets = new int[objects.Length + 1];
            var current = Encoding.ASCII.GetByteCount(header);
            for (var i = 0; i < objects.Length; i++)
            {
                offsets[i + 1] = current;
                builder.Append(objects[i]);
                current += Encoding.ASCII.GetByteCount(objects[i]);
            }

            var xrefOffset = current;
            builder.Append("xref\n");
            builder.AppendFormat(CultureInfo.InvariantCulture, "0 {0}\n", objects.Length + 1);
            builder.Append("0000000000 65535 f \n");
            for (var i = 1; i < offsets.Length; i++)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "{0:0000000000} 00000 n \n", offsets[i]);
            }

            builder.Append("trailer\n<< /Size ");
            builder.Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(" /Root 1 0 R >>\nstartxref\n");
            builder.Append(xrefOffset.ToString(CultureInfo.InvariantCulture));
            builder.Append("\n%%EOF");

            File.WriteAllText(path, builder.ToString(), Encoding.ASCII);
        }

        private static string BuildContentObject()
        {
            var content = "BT\n" +
                          "/F1 12 Tf\n" +
                          "72 720 Td\n" +
                          "(Baseline Characteristics) Tj\n" +
                          "0 -18 Td\n" +
                          "(Group ValueA ValueB) Tj\n" +
                          "0 -18 Td\n" +
                          "(Baseline Control 10 20) Tj\n" +
                          "0 -18 Td\n" +
                          "(Baseline Treatment 15 25) Tj\n" +
                          "0 -18 Td\n" +
                          "(Figure 1 Outcome Response) Tj\n" +
                          "ET\n";

            var length = Encoding.ASCII.GetByteCount(content);
            return $"4 0 obj\n<< /Length {length} >>\nstream\n{content}endstream\nendobj\n";
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
