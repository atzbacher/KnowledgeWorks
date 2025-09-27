#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.DataExtraction;
using LM.Review.Core.DataExtraction;
using Tabula;
using Tabula.Detectors;
using Tabula.Extractors;
using UglyToad.PdfPig;

namespace LM.Infrastructure.Metadata.EvidenceExtraction.Tables
{
    internal sealed class TabulaTableExtractor
    {
        private readonly SimpleNurminenDetectionAlgorithm _detector = new();
        private readonly SpreadsheetExtractionAlgorithm _lattice = new();
        private readonly BasicExtractionAlgorithm _stream = new();
        private readonly TabulaTableImageWriter _imageWriter;

        public TabulaTableExtractor(TabulaTableImageWriter imageWriter)
        {
            _imageWriter = imageWriter ?? throw new ArgumentNullException(nameof(imageWriter));
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

            for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                ct.ThrowIfCancellationRequested();

                var pageArea = ObjectExtractor.ExtractPage(document, pageNumber);
                var candidates = _detector.Detect(pageArea);
                if (candidates.Count == 0)
                {
                    candidates = new List<TableRectangle> { pageArea };
                }

                foreach (var candidate in candidates)
                {
                    ct.ThrowIfCancellationRequested();

                    var area = pageArea.GetArea(candidate.BoundingBox);
                    var tables = ExtractTables(area);
                    foreach (var table in tables)
                    {
                        var rows = NormalizeRows(table);
                        if (!HasMeaningfulData(rows))
                        {
                            continue;
                        }

                        var boundsKey = CreateBoundsKey(pageNumber, table);
                        if (!seen.Add(boundsKey))
                        {
                            continue;
                        }

                        var fileStem = FormattableString.Invariant($"table-{pageNumber:D2}-{index:D2}");
                        var csvPath = Path.Combine(tablesRoot, fileStem + ".csv");
                        await WriteCsvAsync(csvPath, rows, ct).ConfigureAwait(false);

                        var (region, location) = CreateRegions(table, pageArea, pageNumber);
                        var imagePath = await _imageWriter.WriteAsync(docReader, pageNumber, tablesRoot, fileStem, region, ct)
                                                           .ConfigureAwait(false);

                        var columnMappings = BuildColumnMappings(rows.FirstOrDefault() ?? Array.Empty<string>());
                        var rowMappings = BuildRowMappings(rows);
                        var detectedPopulations = ExtractLabels(rowMappings, TableRowRole.Baseline);
                        var detectedEndpoints = ExtractLabels(rowMappings, TableRowRole.Outcome);
                        var classification = DeriveClassification(columnMappings, rowMappings);
                        var tags = BuildTags(classification, detectedPopulations, detectedEndpoints);
                        var friendlyName = FormattableString.Invariant($"Table {pageNumber}-{index:D2}");

                        results.Add(new PreprocessedTable
                        {
                            Title = rows.FirstOrDefault()?.FirstOrDefault() ?? friendlyName,
                            FriendlyName = friendlyName,
                            Classification = classification,
                            Columns = columnMappings,
                            Rows = rowMappings,
                            PageNumbers = new[] { pageNumber },
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
            }

            return results;
        }

        private IReadOnlyList<Table> ExtractTables(PageArea area)
        {
            var results = new List<Table>();
            var lattice = _lattice.Extract(area);
            foreach (var table in lattice)
            {
                if (table.RowCount > 0 && table.ColumnCount > 0)
                {
                    results.Add(table);
                }
            }

            var streamTables = _stream.Extract(area);
            foreach (var table in streamTables)
            {
                if (table.RowCount == 0 || table.ColumnCount == 0)
                {
                    continue;
                }

                if (!results.Any(existing => Intersects(existing, table)))
                {
                    results.Add(table);
                }
            }

            return results;
        }

        private static bool Intersects(Table first, Table second)
        {
            var horizontal = Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left);
            var vertical = Math.Min(first.Top, second.Top) - Math.Max(first.Bottom, second.Bottom);
            return horizontal > 0 && vertical > 0;
        }

        private static string[][] NormalizeRows(Table table)
        {
            return table.Rows
                        .Select(row => row.Select(cell => cell?.GetText()?.Trim() ?? string.Empty).ToArray())
                        .Where(r => r.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                        .ToArray();
        }

        private static bool HasMeaningfulData(IReadOnlyList<string[]> rows)
        {
            if (rows.Count == 0)
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

        private static (TableRegion Region, TablePageLocation Location) CreateRegions(Table table, PageArea pageArea, int pageNumber)
        {
            var pageWidth = pageArea.Width;
            var pageHeight = pageArea.Height;

            var left = Math.Max(0d, table.Left - pageArea.Left);
            var bottom = Math.Max(0d, table.Bottom - pageArea.Bottom);
            var width = Math.Min(pageWidth, table.Width);
            var height = Math.Min(pageHeight, table.Height);

            var normalizedX = pageWidth <= 0d ? 0d : left / pageWidth;
            var normalizedWidth = pageWidth <= 0d ? 0d : width / pageWidth;
            var normalizedHeight = pageHeight <= 0d ? 0d : height / pageHeight;
            var normalizedTopFromBottom = pageHeight <= 0d ? 0d : Math.Max(0d, table.Top - pageArea.Bottom) / pageHeight;
            var normalizedY = 1d - normalizedTopFromBottom;
            if (normalizedY < 0d)
            {
                normalizedY = 0d;
            }
            if (normalizedY + normalizedHeight > 1d)
            {
                normalizedY = Math.Max(0d, 1d - normalizedHeight);
            }

            var region = new TableRegion
            {
                PageNumber = Math.Max(1, pageNumber),
                X = Clamp01(normalizedX),
                Y = Clamp01(normalizedY),
                Width = Clamp01(normalizedWidth),
                Height = Clamp01(normalizedHeight)
            };

            var topFromTop = Math.Max(0d, pageHeight - Math.Max(0d, table.Top - pageArea.Bottom));
            if (pageWidth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(pageWidth));
            if (pageHeight <= 0d)
                throw new ArgumentOutOfRangeException(nameof(pageHeight));

            var location = new TablePageLocation
            {
                PageNumber = Math.Max(1, pageNumber),
                Left = Math.Max(0d, left),
                Top = Math.Max(0d, topFromTop),
                Width = Math.Max(0d, width),
                Height = Math.Max(0d, height),
                PageWidth = pageWidth,
                PageHeight = pageHeight
            };
            return (region, location);
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

        private static string CreateBoundsKey(int pageNumber, Table table)
        {
            return FormattableString.Invariant($"{pageNumber}:{Math.Round(table.Left, 2, MidpointRounding.AwayFromZero)}:{Math.Round(table.Bottom, 2, MidpointRounding.AwayFromZero)}:{Math.Round(table.Width, 2, MidpointRounding.AwayFromZero)}:{Math.Round(table.Height, 2, MidpointRounding.AwayFromZero)}");
        }
    }
}
