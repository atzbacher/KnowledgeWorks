// LM.Infrastructure.Content.PlainTextContentExtractor.cs
#nullable enable
using LM.Core.Abstractions;
using System.Text.RegularExpressions;

namespace LM.Infrastructure.Content
{
    // Public type already exists — we’re improving normalization.
    public sealed class PlainTextContentExtractor : IContentExtractor
    {
        public async Task<string> ExtractTextAsync(string absolutePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return string.Empty;

            var text = await File.ReadAllTextAsync(absolutePath, ct);
            var ext = Path.GetExtension(absolutePath).ToLowerInvariant();

            if (ext == ".md")
            {
                // strip code fences, images/links, headings and common markup glyphs
                text = Regex.Replace(text, @"```.*?```", " ", RegexOptions.Singleline);
                text = Regex.Replace(text, @"!\[[^\]]*\]\([^\)]*\)", " ");
                text = Regex.Replace(text, @"\[[^\]]*\]\([^\)]*\)", " ");
                text = Regex.Replace(text, @"^#{1,6}\s*", "", RegexOptions.Multiline);
                text = Regex.Replace(text, @"[*_`>#~\-]+", " ");
            }

            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim().ToLowerInvariant();
        }
    }
}
