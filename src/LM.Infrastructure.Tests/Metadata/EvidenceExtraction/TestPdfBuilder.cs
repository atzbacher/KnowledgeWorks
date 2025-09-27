#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace LM.Infrastructure.Tests.Metadata.EvidenceExtraction
{
    internal static class TestPdfBuilder
    {
        public static void WriteSimpleBaselinePdf(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must be provided.", nameof(path));

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
    }
}
