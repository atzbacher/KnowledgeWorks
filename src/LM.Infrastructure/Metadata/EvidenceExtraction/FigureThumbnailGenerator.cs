#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

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

            using var surface = SKSurface.Create(new SKImageInfo(400, 300));
            if (surface is null)
                throw new InvalidOperationException("Failed to create placeholder thumbnail surface.");

            var canvas = surface.Canvas;
            if (canvas is null)
                throw new InvalidOperationException("Failed to access placeholder thumbnail canvas.");

            canvas.Clear(new SKColor(240, 244, 248));

            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Png, quality: 100);

            await using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            if (data is null)
                throw new InvalidOperationException("Failed to encode placeholder thumbnail.");

            var bytes = data.ToArray();
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return path;
        }
    }
}
