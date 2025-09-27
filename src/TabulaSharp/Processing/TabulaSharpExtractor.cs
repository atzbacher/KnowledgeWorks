using System;
using System.Collections.Generic;
using System.Linq;
using TabulaSharp.Models;

namespace TabulaSharp.Processing
{
    /// <summary>
    /// Lightweight heuristic table extractor that groups logical lines into a structured table.
    /// </summary>
    public sealed class TabulaSharpExtractor
    {
        private readonly TabulaSharpOptions _options;

        public TabulaSharpExtractor()
            : this(new TabulaSharpOptions())
        {
        }

        public TabulaSharpExtractor(TabulaSharpOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IReadOnlyList<TabulaSharpTable> ExtractTables(IReadOnlyList<TabulaSharpLine> lines)
        {
            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            var ordered = lines.Where(l => l is not null && l.HasTokens)
                               .OrderByDescending(l => l.Baseline)
                               .ToList();
            if (ordered.Count == 0)
            {
                return Array.Empty<TabulaSharpTable>();
            }

            var tables = new List<TabulaSharpTable>();
            var buffer = new List<TabulaSharpLine>();
            var expectedColumns = 0;
            var sawDataRow = false;

            void FlushBuffer()
            {
                if (buffer.Count >= _options.MinimumRows && sawDataRow)
                {
                    var table = BuildTable(buffer);
                    if (table is not null)
                    {
                        tables.Add(table);
                    }
                }

                buffer.Clear();
                expectedColumns = 0;
                sawDataRow = false;
            }

            foreach (var line in ordered)
            {
                var tokenCount = line.Tokens.Count;
                var hasDigits = line.ContainsDigits;

                if (buffer.Count == 0)
                {
                    if (tokenCount >= _options.MinimumHeaderColumns || (tokenCount >= _options.MinimumDataColumns && hasDigits))
                    {
                        buffer.Add(line);
                        expectedColumns = Math.Max(tokenCount, _options.MinimumDataColumns);
                        if (hasDigits)
                        {
                            sawDataRow = true;
                        }
                    }

                    continue;
                }

                if (tokenCount < _options.MinimumDataColumns)
                {
                    FlushBuffer();
                    continue;
                }

                if (hasDigits)
                {
                    sawDataRow = true;
                }

                var closeToExpected = Math.Abs(tokenCount - expectedColumns) <= 1;
                if (!hasDigits && !closeToExpected)
                {
                    FlushBuffer();
                    if (tokenCount >= _options.MinimumHeaderColumns)
                    {
                        buffer.Add(line);
                        expectedColumns = Math.Max(tokenCount, _options.MinimumDataColumns);
                        sawDataRow = hasDigits;
                    }

                    continue;
                }

                buffer.Add(line);
            }

            FlushBuffer();
            return tables;
        }

        private static TabulaSharpTable? BuildTable(List<TabulaSharpLine> lines)
        {
            if (lines.Count == 0)
                return null;

            var header = lines[0];
            var headerTokens = header.Tokens.Where(t => t.HasContent).ToList();
            if (headerTokens.Count == 0)
                return null;

            var anchors = headerTokens.Select(t => t.CenterX).ToArray();
            var columnCount = anchors.Length;
            if (columnCount < 1)
                return null;

            var rows = new List<IReadOnlyList<string>>();
            var tokens = new List<TabulaSharpToken>();

            foreach (var line in lines)
            {
                var rowTokens = line.Tokens.Where(t => t.HasContent).ToList();
                if (rowTokens.Count == 0)
                    continue;

                tokens.AddRange(rowTokens);

                var buckets = CreateBuckets(columnCount);
                foreach (var token in rowTokens)
                {
                    var columnIndex = FindColumnIndex(anchors, token.CenterX);
                    buckets[columnIndex].Add(token.Text);
                }

                var normalized = buckets.Select(b => NormalizeCell(b)).ToArray();
                if (normalized.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                {
                    rows.Add(normalized);
                }
            }

            if (rows.Count <= 1)
                return null;

            var bounds = CalculateBounds(tokens);
            if (!bounds.IsValid)
                return null;

            return new TabulaSharpTable(rows, bounds);
        }

        private static List<List<string>> CreateBuckets(int columnCount)
        {
            var buckets = new List<List<string>>(columnCount);
            for (var i = 0; i < columnCount; i++)
            {
                buckets.Add(new List<string>());
            }

            return buckets;
        }

        private static int FindColumnIndex(double[] anchors, double value)
        {
            var bestIndex = 0;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < anchors.Length; i++)
            {
                var distance = Math.Abs(anchors[i] - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static string NormalizeCell(List<string> tokens)
        {
            if (tokens.Count == 0)
            {
                return string.Empty;
            }

            var joined = string.Join(' ', tokens).Trim();
            while (joined.Contains("  "))
            {
                joined = joined.Replace("  ", " ");
            }

            return joined;
        }

        private static TabulaSharpBoundingBox CalculateBounds(List<TabulaSharpToken> tokens)
        {
            if (tokens.Count == 0)
            {
                return new TabulaSharpBoundingBox(0d, 0d, 0d, 0d);
            }

            var left = tokens.Min(t => t.Left);
            var right = tokens.Max(t => t.Right);
            var bottom = tokens.Min(t => t.Bottom);
            var top = tokens.Max(t => t.Top);
            return new TabulaSharpBoundingBox(left, bottom, right, top);
        }
    }
}
