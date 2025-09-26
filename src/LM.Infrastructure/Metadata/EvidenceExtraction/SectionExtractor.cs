#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LM.Core.Models.DataExtraction;
using UglyToad.PdfPig;

namespace LM.Infrastructure.Metadata.EvidenceExtraction
{
    internal static class SectionExtractor
    {
        public static IReadOnlyList<StructuredSection> Extract(PdfDocument document)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));

            var sections = new List<StructuredSection>();
            var currentHeading = "Document";
            var currentLevel = 1;
            var buffer = new List<string>();
            var pages = new HashSet<int>();

            bool IsHeading(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return false;

                var trimmed = text.Trim();
                if (trimmed.Length < 4)
                    return false;

                if (trimmed.EndsWith(":", StringComparison.Ordinal))
                    return true;

                var upperRatio = trimmed.Count(char.IsUpper) / (double)trimmed.Length;
                return upperRatio > 0.6;
            }

            void Flush()
            {
                if (buffer.Count == 0)
                    return;

                sections.Add(new StructuredSection
                {
                    Heading = currentHeading,
                    Level = currentLevel,
                    Body = string.Join(Environment.NewLine, buffer).Trim(),
                    PageNumbers = pages.OrderBy(p => p).ToArray()
                });

                buffer.Clear();
                pages.Clear();
            }

            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                pages.Add(page.Number);

                foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (IsHeading(line))
                    {
                        Flush();
                        currentHeading = line.Trim().TrimEnd(':');
                        currentLevel = currentHeading.Split('.').Length;
                        buffer.Clear();
                        pages.Add(page.Number);
                        continue;
                    }

                    buffer.Add(line.Trim());
                }
            }

            Flush();
            return sections;
        }
    }
}
