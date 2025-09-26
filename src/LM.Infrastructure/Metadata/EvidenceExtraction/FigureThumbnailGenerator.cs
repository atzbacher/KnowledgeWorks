#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LM.Infrastructure.Metadata.EvidenceExtraction
{
    internal static class FigureThumbnailGenerator
    {
        public static async Task<string> CreatePlaceholderAsync(string figuresRoot, string figureId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(figuresRoot))
                throw new ArgumentException("Figures root must be provided.", nameof(figuresRoot));
            if (string.IsNullOrWhiteSpace(figureId))
                throw new ArgumentException("Figure id must be provided.", nameof(figureId));

            Directory.CreateDirectory(figuresRoot);
            var path = Path.Combine(figuresRoot, $"{figureId}.png");

            using var image = new Image<Rgba32>(400, 300, new Rgba32(240, 244, 248));
            await image.SaveAsync(path, ct).ConfigureAwait(false);
            return path;
        }
    }
}
