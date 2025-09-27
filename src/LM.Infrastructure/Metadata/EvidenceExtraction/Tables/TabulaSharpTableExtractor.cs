#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.DataExtraction;
using LM.Review.Core.DataExtraction;
using TabulaSharp.Models;
using TabulaSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LM.Infrastructure.Metadata.EvidenceExtraction.Tables
{
    public interface IPdfTableExtractor
    {
        Task<IReadOnlyList<PreprocessedTable>> ExtractAsync(PdfDocument document,
                                                             string pdfPath,
                                                             string tablesRoot,
                                                             string hash,
                                                             CancellationToken ct);
    }

    internal sealed class TabulaSharpTableExtractor : IPdfTableExtractor
    {
        private readonly TabulaSharpExtractor _extractor;
        private readonly TabulaTableImageWriter _imageWriter;

        public TabulaSharpTableExtractor(TabulaTableImageWriter imageWriter)
        {
            _imageWriter = imageWriter ?? throw new ArgumentNullException(nameof(imageWriter));
            _extractor = new TabulaSharpExtractor();
        }

        public async Task<IReadOnlyList<PreprocessedTable>> ExtractAsync(PdfDocument document,
                                                                         string pdfPath,
                                                                         string tablesRoot,
                                                                         string hash,
                                                                         CancellationToken ct)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                throw new ArgumentException("PDF path must point to an existing file.", nameof(pdfPath));
            if (string.IsNullOrWhiteSpace(tablesRoot))
                throw new ArgumentException("Tables root must be provided.", nameof(tablesRoot));
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash must be provided.", nameof(hash));

            Directory.CreateDirectory(tablesRoot);
            var results = new List<PreprocessedTable>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var index = 1;

            using var docReader = _imageWriter.CreateDocumentReader(pdfPath);

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

                foreach (var table in tables)
                {
                    ct.ThrowIfCancellationRequested();

                    var rows = NormalizeRows(table.Rows);
                    if (!HasMeaningfulData(rows))
                    {
                        continue;
                    }

                    var boundsKey = CreateBoundsKey(page.Number, table.Bounds);
                    if (!seen.Add(boundsKey))
                    {
                        continue;
                    }

                    var fileStem = FormattableString.Invariant($"table-{page.Number:D2}-{index:D2}");
                    var csvPath = Path.Combine(tablesRoot, fileStem + ".csv");
                    await WriteCsvAsync(csvPath, rows, ct).ConfigureAwait(false);

                    var (region, location) = CreateRegions(table.Bounds, page);
                    var imagePath = await _imageWriter.WriteAsync(docReader, page.Number, tablesRoot, fileStem, region, ct)
                                                       .ConfigureAwait(false);

                    var columnMappings = BuildColumnMappings(rows.FirstOrDefault() ?? Array.Empty<string>());
                    var rowMappings = BuildRowMappings(rows);
                    var detectedPopulations = ExtractLabels(rowMappings, TableRowRole.Baseline);
                    var detectedEndpoints = ExtractLabels(rowMappings, TableRowRole.Outcome);
                    var classification = DeriveClassification(columnMappings, rowMappings);
                    var tags = BuildTags(classification, detectedPopulations, detectedEndpoints);
                    var friendlyName = FormattableString.Invariant($"Table {page.Number}-{index:D2}");

                    results.Add(new PreprocessedTable
                    {
                        Title = rows.FirstOrDefault()?.FirstOrDefault() ?? friendlyName,
                        FriendlyName = friendlyName,
                        Classification = classification,
                        Columns = columnMappings,
                        Rows = rowMappings,
                        PageNumbers = new[] { page.Number },
                        CsvRelativePath = csvPath,
                        ImageRelativePath = imagePath,
                        DetectedPopulations = detectedPopulations,
                        DetectedEndpoints = detectedEndpoints,
                        Tags = tags,
                        Regions = new[] { region },
                        PageLocations = new[] { location },
                        ProvenanceHash = ComputeProvenance(hash, Path.GetFileName(csvPath) ?? string.Empty),
                        ImageProvenanceHash = ComputeProvenance(hash, Path.GetFileName(imagePath) ?? string.Empty)
                    });

                    index++;
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

        private static string[][] NormalizeRows(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            return rows.Select(row => row.Select(cell => cell?.Trim() ?? string.Empty).ToArray())
                       .Where(r => r.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                       .ToArray();
        }

        private static bool HasMeaningfulData(IReadOnlyList<string[]> rows)
        {
            if (rows.Count == 0)
            {
                return false;
            }

            if (rows.Count < 2)
            {
                return false;
            }

            var maxColumns = rows.Max(r => r.Length);
            if (maxColumns == 0)
            {
                return false;
            }

            var nonEmptyCells = rows.Sum(r => r.Count(cell => !string.IsNullOrWhiteSpace(cell)));
            return nonEmptyCells > maxColumns;
        }

        private static async Task WriteCsvAsync(string path, IReadOnlyList<string[]> rows, CancellationToken ct)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                var escaped = row.Select(EscapeCell);
                builder.AppendLine(string.Join(',', escaped));
            }

            await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
        }

        private static (TableRegion Region, TablePageLocation Location) CreateRegions(TabulaSharpBoundingBox bounds, Page page)
        {
            var pageWidth = page.Width;
            var pageHeight = page.Height;

            var left = Math.Max(0d, bounds.Left);
            var bottom = Math.Max(0d, bounds.Bottom);
            var width = Math.Max(0d, bounds.Width);
            var height = Math.Max(0d, bounds.Height);
            if (width <= 0d || height <= 0d)
            {
                left = 0d;
                bottom = 0d;
                width = pageWidth;
                height = pageHeight;
            }

            var top = bottom + height;
            var normalizedX = pageWidth <= 0d ? 0d : Clamp01(left / pageWidth);
            var normalizedWidth = pageWidth <= 0d ? 0d : Clamp01(width / pageWidth);
            var normalizedY = pageHeight <= 0d ? 0d : Clamp01((pageHeight - top) / pageHeight);
            var normalizedHeight = pageHeight <= 0d ? 0d : Clamp01(height / pageHeight);
            if (normalizedY + normalizedHeight > 1d)
            {
                normalizedY = Math.Max(0d, 1d - normalizedHeight);
            }

            var region = new TableRegion
            {
                PageNumber = page.Number,
                X = normalizedX,
                Y = normalizedY,
                Width = normalizedWidth,
                Height = normalizedHeight
            };

            var location = new TablePageLocation
            {
                PageNumber = page.Number,
                Left = left,
                Top = Math.Max(0d, pageHeight - top),
                Width = width,
                Height = height,
                PageWidth = pageWidth,
                PageHeight = pageHeight
            };

            return (region, location);
        }

        private static IReadOnlyList<TableColumnMapping> BuildColumnMappings(IReadOnlyList<string> headerRow)
        {
            var mappings = new List<TableColumnMapping>();
            for (var i = 0; i < headerRow.Count; i++)
            {
                var header = headerRow[i];
                var role = TableVocabulary.ClassifyColumnHeader(header);
                mappings.Add(new TableColumnMapping
                {
                    ColumnIndex = i,
                    Header = header,
                    Role = role,
                    NormalizedHeader = TableVocabulary.NormalizeHeader(header)
                });
            }

            return mappings;
        }

        private static IReadOnlyList<TableRowMapping> BuildRowMappings(IReadOnlyList<string[]> rows)
        {
            var mappings = new List<TableRowMapping>();
            for (var i = 0; i < rows.Count; i++)
            {
                var cells = rows[i];
                var label = cells.Length > 0 ? cells[0] : string.Empty;
                var role = i == 0 ? TableRowRole.Header : TableVocabulary.ClassifyRowLabel(label);
                mappings.Add(new TableRowMapping
                {
                    RowIndex = i,
                    Label = label,
                    Role = role
                });
            }

            return mappings;
        }

        private static TableClassificationKind DeriveClassification(IReadOnlyList<TableColumnMapping> columns,
                                                                    IReadOnlyList<TableRowMapping> rows)
        {
            var hasBaseline = rows.Any(r => r.Role == TableRowRole.Baseline);
            var hasOutcome = rows.Any(r => r.Role == TableRowRole.Outcome);
            if (hasBaseline && hasOutcome)
                return TableClassificationKind.Mixed;
            if (hasOutcome)
                return TableClassificationKind.Outcome;
            if (hasBaseline)
                return TableClassificationKind.Baseline;

            var header = columns.FirstOrDefault()?.Header;
            return TableVocabulary.ClassifyTitle(header);
        }

        private static IReadOnlyList<string> ExtractLabels(IReadOnlyList<TableRowMapping> rows, TableRowRole role)
        {
            return rows.Where(r => r.Role == role)
                       .Select(r => r.Label)
                       .Where(label => !string.IsNullOrWhiteSpace(label))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToArray();
        }

        private static IReadOnlyList<string> BuildTags(TableClassificationKind classification,
                                                       IReadOnlyList<string> populations,
                                                       IReadOnlyList<string> endpoints)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (classification != TableClassificationKind.Unknown)
            {
                tags.Add(classification.ToString());
            }

            foreach (var population in populations)
            {
                tags.Add(population);
            }

            foreach (var endpoint in endpoints)
            {
                tags.Add(endpoint);
            }

            var ordered = tags.ToList();
            ordered.Sort(StringComparer.OrdinalIgnoreCase);
            return ordered;
        }

        private static string EscapeCell(string value)
        {
            value ??= string.Empty;
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            {
                value = value.Replace("\"", "\"\"");
                return FormattableString.Invariant($"\"{value}\"");
            }

            return value;
        }

        private static string ComputeProvenance(string hash, string fileName)
        {
            var input = Encoding.UTF8.GetBytes(FormattableString.Invariant($"{hash}:{fileName}"));
            var bytes = System.Security.Cryptography.SHA256.HashData(input);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string CreateBoundsKey(int pageNumber, TabulaSharpBoundingBox bounds)
        {
            var left = Math.Round(bounds.Left, 2, MidpointRounding.AwayFromZero);
            var bottom = Math.Round(bounds.Bottom, 2, MidpointRounding.AwayFromZero);
            var width = Math.Round(bounds.Width, 2, MidpointRounding.AwayFromZero);
            var height = Math.Round(bounds.Height, 2, MidpointRounding.AwayFromZero);

            return FormattableString.Invariant($"{pageNumber}:{left}:{bottom}:{width}:{height}");
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value))
                return 0d;
            if (value < 0d)
                return 0d;
            if (value > 1d)
                return 1d;
            return value;
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
