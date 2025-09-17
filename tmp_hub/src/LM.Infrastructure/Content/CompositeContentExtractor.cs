// LM.Infrastructure.Content.CompositeContentExtractor.cs
#nullable enable
using LM.Core.Abstractions;

namespace LM.Infrastructure.Content
{
    // Public class already in API — just orchestrate the existing extractors.
    public sealed class CompositeContentExtractor : IContentExtractor
    {
        private readonly OpenXmlContentExtractor _ooxml = new();
        private readonly PlainTextContentExtractor _plain = new();
        private readonly PdfPigContentExtractor _pdf = new();

        public async Task<string> ExtractTextAsync(string absolutePath, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(absolutePath).ToLowerInvariant();
            return ext switch
            {
                ".docx" or ".pptx" => await _ooxml.ExtractTextAsync(absolutePath, ct),
                ".txt" or ".md" => await _plain.ExtractTextAsync(absolutePath, ct),
                ".pdf" => await _pdf.ExtractTextAsync(absolutePath, ct),
                _ => string.Empty, // unsupported → no noise
            };
        }
    }
}
