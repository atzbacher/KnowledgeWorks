#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.TabulaSharp.Models;
using TabulaSharp.Models;
using TabulaSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LM.App.Wpf.ViewModels.TabulaSharp.Services
{
    internal sealed class TabulaSharpPlaygroundExtractor
    {
        private readonly TabulaSharpExtractor _extractor = new();

        public Task<IReadOnlyList<TabulaSharpPlaygroundTableResult>> ExtractAsync(string pdfPath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(pdfPath))
                throw new ArgumentException("PDF path must be provided.", nameof(pdfPath));

            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF file not found.", pdfPath);

            return Task.Run(() => ExtractInternal(pdfPath, ct), ct);
        }

        private IReadOnlyList<TabulaSharpPlaygroundTableResult> ExtractInternal(string pdfPath, CancellationToken ct)
        {
            var results = new List<TabulaSharpPlaygroundTableResult>();

            using var document = PdfDocument.Open(pdfPath);
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                var lines = BuildLines(page);
                if (lines.Count == 0)
                {
                    continue;
                }

                var tables = _extractor.ExtractTables(lines);
                if (tables.Count == 0)
                {
                    continue;
                }

                var tableIndex = 1;
                foreach (var table in tables)
                {
                    ct.ThrowIfCancellationRequested();

                    var normalized = NormalizeRows(table.Rows);
                    if (normalized.Count < 2)
                    {
                        continue;
                    }

                    results.Add(new TabulaSharpPlaygroundTableResult(page.Number, tableIndex, table.Bounds, normalized));
                    tableIndex++;
                }
            }

            return results;
        }

        private static IReadOnlyList<TabulaSharpLine> BuildLines(Page page)
        {
            var words = page.GetWords();
            var buffers = new List<LineBuffer>();
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }

                var centerY = (word.BoundingBox.Bottom + word.BoundingBox.Top) / 2d;
                var buffer = FindOrCreateBuffer(buffers, centerY);
                buffer.Add(word);
            }

            return buffers.Select(buffer => buffer.ToLine()).ToArray();
        }

        private static LineBuffer FindOrCreateBuffer(List<LineBuffer> buffers, double centerY)
        {
            const double tolerance = 3d;
            foreach (var buffer in buffers)
            {
                if (Math.Abs(buffer.CenterY - centerY) <= tolerance)
                {
                    return buffer;
                }
            }

            var created = new LineBuffer(centerY);
            buffers.Add(created);
            return created;
        }

        private static IReadOnlyList<string[]> NormalizeRows(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            return rows.Select(row => row.Select(cell => cell?.Trim() ?? string.Empty).ToArray())
                       .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                       .ToArray();
        }

        private sealed class LineBuffer
        {
            private readonly List<Word> _words = new();

            public LineBuffer(double centerY)
            {
                CenterY = centerY;
            }

            public double CenterY { get; private set; }

            public void Add(Word word)
            {
                _words.Add(word);
                var y = (word.BoundingBox.Bottom + word.BoundingBox.Top) / 2d;
                CenterY = (CenterY + y) / 2d;
            }

            public TabulaSharpLine ToLine()
            {
                var ordered = _words.OrderBy(w => w.BoundingBox.Left)
                                     .Select(w => new TabulaSharpToken(w.Text,
                                                                       w.BoundingBox.Left,
                                                                       w.BoundingBox.Bottom,
                                                                       w.BoundingBox.Right,
                                                                       w.BoundingBox.Top))
                                     .Where(t => t.HasContent)
                                     .ToArray();

                return new TabulaSharpLine(CenterY, ordered);
            }
        }
    }
}
