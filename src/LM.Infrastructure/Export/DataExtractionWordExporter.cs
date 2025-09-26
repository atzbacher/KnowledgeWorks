#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Export
{
    public sealed class DataExtractionWordExporter : IDataExtractionWordExporter
    {
        private readonly DataExtractionExportLoader _loader;

        public DataExtractionWordExporter(DataExtractionExportLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public Task<bool> CanExportAsync(string entryId, CancellationToken ct = default)
            => _loader.HasExtractionAsync(entryId, ct);

        public async Task<string> ExportAsync(string entryId, string outputPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry id must be provided.", nameof(entryId));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            var context = await _loader.LoadAsync(entryId, ct).ConfigureAwait(false);
            if (context is null)
            {
                throw new InvalidOperationException($"No data extraction hook found for entry '{entryId}'.");
            }

            using var document = WordprocessingDocument.Create(outputPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            AppendParagraph(body, string.IsNullOrWhiteSpace(context.Hub.DisplayTitle) ? context.EntryId : context.Hub.DisplayTitle, bold: true, size: 32);
            AppendParagraph(body, BuildHeaderLine(context));

            var endpointLookup = BuildNameLookup(context.Extraction.Endpoints, e => e.Id, e => e.Name);
            var interventionLookup = BuildNameLookup(context.Extraction.Interventions, i => i.Id, i => i.Name);

            foreach (var table in context.Extraction.Tables)
            {
                ct.ThrowIfCancellationRequested();
                AppendTableSection(body, context, table, endpointLookup, interventionLookup);
            }

            mainPart.Document.Save();
            return outputPath;
        }

        private static void AppendTableSection(Body body,
                                                DataExtractionExportContext context,
                                                HookM.DataExtractionTable table,
                                                IReadOnlyDictionary<string, string> endpointLookup,
                                                IReadOnlyDictionary<string, string> interventionLookup)
        {
            var title = string.IsNullOrWhiteSpace(table.Title) ? "Table" : table.Title;
            AppendParagraph(body, title, bold: true, size: 26);

            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                AppendParagraph(body, "Caption: " + table.Caption);
            }

            var rows = ReadCsv(context.TryResolveAbsolutePath(table.SourcePath));
            var tableElement = new Table(new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 8 },
                    new LeftBorder { Val = BorderValues.Single, Size = 8 },
                    new BottomBorder { Val = BorderValues.Single, Size = 8 },
                    new RightBorder { Val = BorderValues.Single, Size = 8 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 8 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 8 })));

            if (rows.Count == 0)
            {
                tableElement.Append(new TableRow(new TableCell(new Paragraph(new Run(new Text("No CSV data found") { Space = SpaceProcessingModeValues.Preserve })))));
            }
            else
            {
                foreach (var row in rows)
                {
                    var tableRow = new TableRow();
                    foreach (var cell in row)
                    {
                        tableRow.Append(new TableCell(new Paragraph(new Run(new Text(cell ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }))));
                    }

                    tableElement.Append(tableRow);
                }
            }

            body.Append(tableElement);

            var metadata = new List<string>
            {
                FormatPages(table.Pages),
                "Endpoints: " + FormatLinked(table.LinkedEndpointIds, endpointLookup),
                "Interventions: " + FormatLinked(table.LinkedInterventionIds, interventionLookup),
                "Provenance hash: " + (string.IsNullOrWhiteSpace(table.ProvenanceHash) ? "(missing)" : table.ProvenanceHash)
            };

            if (!string.IsNullOrWhiteSpace(table.Summary))
            {
                metadata.Add("Summary: " + table.Summary);
            }

            AppendParagraph(body, string.Join(Environment.NewLine, metadata));
            AppendParagraph(body, BuildReferenceLine(context));
        }

        private static void AppendParagraph(Body body, string text, bool bold = false, int size = 24)
        {
            var run = new Run(new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
            if (bold)
            {
                run.PrependChild(new RunProperties(new Bold(), new FontSize { Val = size.ToString(CultureInfo.InvariantCulture) }));
            }
            else if (size != 24)
            {
                run.PrependChild(new RunProperties(new FontSize { Val = size.ToString(CultureInfo.InvariantCulture) }));
            }

            var paragraph = new Paragraph(run);
            body.Append(paragraph);
        }

        private static string BuildHeaderLine(DataExtractionExportContext context)
        {
            var parts = new List<string>
            {
                $"Entry ID: {context.EntryId}",
                string.Create(CultureInfo.InvariantCulture, $"Extracted: {context.Extraction.ExtractedAtUtc:yyyy-MM-dd HH:mm} UTC by {context.Extraction.ExtractedBy}")
            };

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.DOI))
            {
                parts.Add("DOI: " + context.Article!.Identifier.DOI);
            }

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.PMID))
            {
                parts.Add("PMID: " + context.Article!.Identifier.PMID);
            }

            return string.Join(" | ", parts);
        }

        private static string BuildReferenceLine(DataExtractionExportContext context)
        {
            var parts = new List<string>
            {
                string.Create(CultureInfo.InvariantCulture, $"Extracted on {context.Extraction.ExtractedAtUtc:yyyy-MM-dd}")
            };

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.DOI))
            {
                parts.Add("DOI: " + context.Article!.Identifier.DOI);
            }

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.PMID))
            {
                parts.Add("PMID: " + context.Article!.Identifier.PMID);
            }

            return string.Join(" | ", parts);
        }

        private static string FormatPages(IReadOnlyCollection<string> pages)
        {
            if (pages is null || pages.Count == 0)
            {
                return "Pages: (unspecified)";
            }

            return "Pages: " + string.Join(", ", pages);
        }

        private static string FormatLinked(IEnumerable<string> ids, IReadOnlyDictionary<string, string> lookup)
        {
            if (ids is null)
            {
                return "(none)";
            }

            var names = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => lookup.TryGetValue(id, out var value) ? value : id)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            return names.Length == 0 ? "(none)" : string.Join(", ", names);
        }

        private static Dictionary<string, string> BuildNameLookup<T>(IEnumerable<T> source, Func<T, string> keySelector, Func<T, string> valueSelector)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in source)
            {
                if (item is null)
                {
                    continue;
                }

                var key = keySelector(item);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map[key] = valueSelector(item);
            }

            return map;
        }

        private static IReadOnlyList<string[]> ReadCsv(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return Array.Empty<string[]>();
            }

            var rows = new List<string[]>();
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    continue;
                }

                rows.Add(ParseCsvLine(line));
            }

            return rows;
        }

        private static string[] ParseCsvLine(string line)
        {
            var cells = new List<string>();
            var builder = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        builder.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    cells.Add(builder.ToString().Trim());
                    builder.Clear();
                }
                else
                {
                    builder.Append(ch);
                }
            }

            cells.Add(builder.ToString().Trim());
            return cells.ToArray();
        }
    }
}
