#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TabulaSharp.Models;
using TabulaSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LM.App.Wpf.ViewModels.Playground
{
    internal sealed class TabulaSharpPlaygroundEngine
    {
        public Task<IReadOnlyList<TabulaSharpPlaygroundTable>> ExtractAsync(string pdfPath,
                                                                           int pageNumber,
                                                                           TabulaSharpOptions options,
                                                                           CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(pdfPath))
                throw new ArgumentException("PDF path must be provided.", nameof(pdfPath));

            var optionsToUse = options ?? new TabulaSharpOptions();
            return Task.Run(() => ExtractInternal(pdfPath, pageNumber, optionsToUse), ct);
        }

        private static IReadOnlyList<TabulaSharpPlaygroundTable> ExtractInternal(string pdfPath,
                                                                                  int pageNumber,
                                                                                  TabulaSharpOptions options)
        {
            using var document = PdfDocument.Open(pdfPath);
            if (document.NumberOfPages <= 0)
            {
                return Array.Empty<TabulaSharpPlaygroundTable>();
            }

            var normalizedPage = Math.Clamp(pageNumber, 1, document.NumberOfPages);
            var page = document.GetPage(normalizedPage);
            var lines = BuildLines(page);
            var extractor = new TabulaSharpExtractor(options);
            var tables = extractor.ExtractTables(lines);

            var results = new List<TabulaSharpPlaygroundTable>();
            var index = 1;
            foreach (var table in tables)
            {
                var normalizedRows = NormalizeRows(table.Rows);
                if (normalizedRows.Count == 0)
                {
                    continue;
                }

                results.Add(new TabulaSharpPlaygroundTable(normalizedPage, index++, normalizedRows));
            }

            return results;
        }

        private static List<string[]> NormalizeRows(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            return rows.Select(row => row.Select(cell => cell?.Trim() ?? string.Empty).ToArray())
                       .Where(r => r.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                       .ToList();
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
            const double Tolerance = 3d;
            foreach (var buffer in buffers)
            {
                if (Math.Abs(buffer.CenterY - centerY) <= Tolerance)
                {
                    return buffer;
                }
            }

            var created = new LineBuffer(centerY);
            buffers.Add(created);
            return created;
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
                                     .Where(token => token.HasContent)
                                     .ToArray();

                return new TabulaSharpLine(CenterY, ordered);
            }
        }
    }
}
