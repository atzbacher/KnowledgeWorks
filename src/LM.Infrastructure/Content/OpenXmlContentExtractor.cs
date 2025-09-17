// LM.Infrastructure.Content.OpenXmlContentExtractor.cs
#nullable enable
using LM.Core.Abstractions;
using System.IO.Compression;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LM.Infrastructure.Content
{
    // Public type already exists in your API — we’re only replacing internals.
    public sealed class OpenXmlContentExtractor : IContentExtractor
    {
        // <a:t> (pptx) and <w:t> (docx) namespaces
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

        // matches "12" or "12/34" etc. → common slide numbers
        private static readonly Regex SlideNumberLike = new(@"^\s*\d+(\s*/\s*\d+)?\s*$", RegexOptions.Compiled);

        public async Task<string> ExtractTextAsync(string absolutePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return string.Empty;

            var ext = Path.GetExtension(absolutePath).ToLowerInvariant();
            if (ext != ".docx" && ext != ".pptx") return string.Empty;

            using var zip = ZipFile.OpenRead(absolutePath);

            return ext switch
            {
                ".docx" => await ExtractDocxAsync(zip, ct),
                ".pptx" => await ExtractPptxAsync(zip, ct),
                _ => string.Empty
            };
        }

        private static Task<string> ExtractDocxAsync(ZipArchive zip, CancellationToken _)
        {
            // Only the body document (skip headers/footers to avoid boilerplate)
            var e = zip.GetEntry("word/document.xml");
            if (e is null) return Task.FromResult(string.Empty);

            using var s = e.Open();
            var x = XDocument.Load(s, LoadOptions.PreserveWhitespace);

            var sb = new StringBuilder(4096);
            foreach (var t in x.Descendants(W + "t"))
            {
                var text = (string?)t;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append(text);
                    sb.Append(' ');
                }
            }
            return Task.FromResult(Normalize(sb.ToString()));
        }

        private static Task<string> ExtractPptxAsync(ZipArchive zip, CancellationToken _)
        {
            // Collect visible text from slides only; ignore masters/layouts
            var slideXml = zip.Entries
                .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                         && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var perSlide = new List<List<string>>(slideXml.Count);

            foreach (var entry in slideXml)
            {
                using var s = entry.Open();
                var x = XDocument.Load(s, LoadOptions.PreserveWhitespace);

                // a:t nodes carry user-visible text
                var lines = x.Descendants(A + "t")
                             .Select(n => ((string?)n)?.Trim())
                             .Where(v => !string.IsNullOrWhiteSpace(v))
                             .Select(v => v!)
                             .ToList();

                perSlide.Add(lines);
            }

            // Remove footers/boilerplate lines that repeat on most slides + plain slide numbers
            var linesFiltered = RemoveBoilerplate(perSlide);

            var sb = new StringBuilder(4096);
            foreach (var line in linesFiltered)
            {
                sb.Append(line);
                sb.Append(' ');
            }

            return Task.FromResult(Normalize(sb.ToString()));
        }

        private static IEnumerable<string> RemoveBoilerplate(List<List<string>> perSlide)
        {
            var slideCount = Math.Max(1, perSlide.Count);
            var freq = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var slide in perSlide)
            {
                foreach (var line in slide)
                {
                    if (SlideNumberLike.IsMatch(line)) continue;
                    if (line.Length <= 2) continue; // throw away micro-fragments
                    freq[line] = freq.TryGetValue(line, out var c) ? c + 1 : 1;
                }
            }

            // “Appears on >=60% of slides” → assume footer/header-ish
            var threshold = Math.Max(2, (int)Math.Ceiling(slideCount * 0.6));
            var boiler = new HashSet<string>(freq.Where(kv => kv.Value >= threshold)
                                                 .Select(kv => kv.Key),
                                             StringComparer.Ordinal);

            foreach (var slide in perSlide)
            {
                foreach (var line in slide)
                {
                    if (boiler.Contains(line)) continue;
                    if (SlideNumberLike.IsMatch(line)) continue;
                    yield return line;
                }
            }
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // collapse whitespace + ASCII lowercase for stable shingles
            var collapsed = Regex.Replace(s, @"\s+", " ");
            return collapsed.Trim().ToLowerInvariant();
        }
    }
}
