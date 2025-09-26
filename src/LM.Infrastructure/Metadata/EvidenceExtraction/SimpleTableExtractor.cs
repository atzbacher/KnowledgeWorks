#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LM.Core.Models.DataExtraction;
using LM.Review.Core.DataExtraction;
using UglyToad.PdfPig;

namespace LM.Infrastructure.Metadata.EvidenceExtraction
{
    internal sealed class SimpleTableExtractor
    {
        public IReadOnlyList<PreprocessedTable> Extract(PdfDocument document, string tablesRoot, string hash)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(tablesRoot))
                throw new ArgumentException("Tables root must be provided.", nameof(tablesRoot));

            Directory.CreateDirectory(tablesRoot);
            var results = new List<PreprocessedTable>();
            var index = 1;

            foreach (var page in document.GetPages())
            {
                var lines = page.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(l => l.Trim())
                                      .Where(l => l.Length > 0)
                                      .ToArray();

                if (lines.Length == 0)
                    continue;

                var tableLines = new List<string[]>();

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && parts.Count(p => p.Any(char.IsDigit)) >= 1)
                    {
                        tableLines.Add(parts);
                    }
                    else if (tableLines.Count > 0)
                    {
                        BuildTableFromBuffer(tableLines, page.Number, results, tablesRoot, hash, ref index);
                        tableLines.Clear();
                    }
                }

                if (tableLines.Count > 0)
                {
                    BuildTableFromBuffer(tableLines, page.Number, results, tablesRoot, hash, ref index);
                }
            }

            return results;
        }

        private static void BuildTableFromBuffer(List<string[]> buffer,
                                                 int pageNumber,
                                                 List<PreprocessedTable> results,
                                                 string tablesRoot,
                                                 string hash,
                                                 ref int index)
        {
            if (buffer.Count == 0)
                return;

            var csv = new StringBuilder();
            foreach (var row in buffer)
            {
                var escaped = row.Select(EscapeCell);
                csv.AppendLine(string.Join(',', escaped));
            }

            var fileName = $"table-{index.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}.csv";
            var absolute = Path.Combine(tablesRoot, fileName);
            File.WriteAllText(absolute, csv.ToString());

            var columns = BuildColumnMappings(buffer.FirstOrDefault());
            var rows = BuildRowMappings(buffer);

            var detectedPopulations = rows.Where(r => r.Role == TableRowRole.Baseline)
                                          .Select(r => r.Label)
                                          .Where(l => !string.IsNullOrWhiteSpace(l))
                                          .Distinct(StringComparer.OrdinalIgnoreCase)
                                          .ToArray();

            var detectedEndpoints = rows.Where(r => r.Role == TableRowRole.Outcome)
                                        .Select(r => r.Label)
                                        .Where(l => !string.IsNullOrWhiteSpace(l))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToArray();

            var classification = DeriveClassification(columns, rows);

            var table = new PreprocessedTable
            {
                Title = buffer.FirstOrDefault()?.FirstOrDefault() ?? $"Table {index}",
                Classification = classification,
                Columns = columns,
                Rows = rows,
                PageNumbers = new[] { pageNumber },
                CsvRelativePath = absolute,
                DetectedPopulations = detectedPopulations,
                DetectedEndpoints = detectedEndpoints,
                ProvenanceHash = ComputeProvenance(hash, fileName)
            };

            results.Add(table);
            index++;
        }

        private static IReadOnlyList<TableColumnMapping> BuildColumnMappings(string[]? headerRow)
        {
            var mappings = new List<TableColumnMapping>();
            if (headerRow is null)
                return mappings;

            for (var i = 0; i < headerRow.Length; i++)
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

        private static IReadOnlyList<TableRowMapping> BuildRowMappings(List<string[]> rows)
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

        private static TableClassificationKind DeriveClassification(IReadOnlyList<TableColumnMapping> columns, IReadOnlyList<TableRowMapping> rows)
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

        private static string EscapeCell(string value)
        {
            value ??= string.Empty;
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
        }

        private static string ComputeProvenance(string hash, string fileName)
        {
            var input = Encoding.UTF8.GetBytes($"{hash}:{fileName}");
            var bytes = System.Security.Cryptography.SHA256.HashData(input);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
