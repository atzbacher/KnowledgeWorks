#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using LM.Core.Models.DataExtraction;
using SkiaSharp;

namespace LM.Infrastructure.Metadata.EvidenceExtraction.Tables
{
    public sealed class TabulaTableImageWriter
    {
        private readonly PageDimensions _dimensions;

        public TabulaTableImageWriter(double scalingFactor = 2.0)
        {
            if (scalingFactor <= 0d)
                throw new ArgumentOutOfRangeException(nameof(scalingFactor));

            _dimensions = new PageDimensions(scalingFactor);
        }

        public IDocReader CreateDocumentReader(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath))
                throw new ArgumentException("PDF path must be provided.", nameof(pdfPath));

            return DocLib.Instance.GetDocReader(pdfPath, _dimensions);
        }

        public async Task<string> WriteAsync(IDocReader docReader,
                                             int pageNumber,
                                             string tablesRoot,
                                             string fileStem,
                                             TableRegion region,
                                             CancellationToken ct)
        {
            if (docReader is null)
                throw new ArgumentNullException(nameof(docReader));
            if (string.IsNullOrWhiteSpace(tablesRoot))
                throw new ArgumentException("Tables root must be provided.", nameof(tablesRoot));
            if (string.IsNullOrWhiteSpace(fileStem))
                throw new ArgumentException("File stem must be provided.", nameof(fileStem));

            Directory.CreateDirectory(tablesRoot);

            using var pageReader = docReader.GetPageReader(pageNumber - 1);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var buffer = pageReader.GetImage();

            if (width <= 0 || height <= 0 || buffer.Length == 0)
                throw new InvalidOperationException("Unable to render PDF page for table snapshot.");

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);
            Marshal.Copy(buffer, 0, bitmap.GetPixels(), buffer.Length);

            var crop = CreateCropRectangle(info, region);
            using var cropped = new SKBitmap(crop.Width, crop.Height, info.ColorType, info.AlphaType);
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, crop, new SKRect(0, 0, crop.Width, crop.Height));
                canvas.Flush();
            }

            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
                throw new InvalidOperationException("Failed to encode table snapshot.");

            var path = Path.Combine(tablesRoot, fileStem + ".png");
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            var pngBytes = data.ToArray();
            await stream.WriteAsync(pngBytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return path;
        }

        private static SKRectI CreateCropRectangle(SKImageInfo info, TableRegion region)
        {
            var x = (int)Math.Round(region.X * info.Width, MidpointRounding.AwayFromZero);
            var y = (int)Math.Round(region.Y * info.Height, MidpointRounding.AwayFromZero);
            var width = (int)Math.Round(region.Width * info.Width, MidpointRounding.AwayFromZero);
            var height = (int)Math.Round(region.Height * info.Height, MidpointRounding.AwayFromZero);

            if (width <= 0 || height <= 0)
            {
                x = 0;
                y = 0;
                width = info.Width;
                height = info.Height;
            }

            x = Math.Clamp(x, 0, Math.Max(0, info.Width - 1));
            y = Math.Clamp(y, 0, Math.Max(0, info.Height - 1));

            var maxWidth = info.Width - x;
            var maxHeight = info.Height - y;
            width = Math.Clamp(width, 1, maxWidth);
            height = Math.Clamp(height, 1, maxHeight);

            return new SKRectI(x, y, x + width, y + height);
        }
    }
}
