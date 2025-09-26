#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models.DataExtraction;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Export
{
    public sealed class DataExtractionExcelExporter : IDataExtractionExcelExporter
    {
        private static readonly object s_licenseLock = new();
        private static bool s_licenseApplied;

        private readonly DataExtractionExportLoader _loader;

        public DataExtractionExcelExporter(DataExtractionExportLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            EnsureLicense();
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

            using var package = new ExcelPackage();
            var workbook = package.Workbook;

            var endpointLookup = BuildNameLookup(context.Extraction.Populations, p => p.Id, p => p.Label);
            var interventionLookup = BuildNameLookup(context.Extraction.Interventions, i => i.Id, i => i.Name);

            var endpointsSheet = workbook.Worksheets.Add("Endpoints");
            WriteMetadataHeader(endpointsSheet, context);
            WriteEndpointsSheet(endpointsSheet, context.Extraction.Endpoints, endpointLookup, interventionLookup);

            var baselinesSheet = workbook.Worksheets.Add("Baselines");
            WriteMetadataHeader(baselinesSheet, context);
            WriteBaselinesSheet(baselinesSheet, context.Extraction.Tables);

            package.SaveAs(new FileInfo(outputPath));
            return outputPath;
        }

        private static void WriteMetadataHeader(ExcelWorksheet sheet, DataExtractionExportContext context)
        {
            var row = 1;
            sheet.Cells[row++, 1].Value = string.IsNullOrWhiteSpace(context.Hub.DisplayTitle) ? context.EntryId : context.Hub.DisplayTitle;
            sheet.Cells[row++, 1].Value = string.Create(CultureInfo.InvariantCulture, $"Entry ID: {context.EntryId}");
            sheet.Cells[row++, 1].Value = string.Create(CultureInfo.InvariantCulture, $"Extracted: {context.Extraction.ExtractedAtUtc:yyyy-MM-dd HH:mm} UTC by {context.Extraction.ExtractedBy}");

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.DOI))
            {
                sheet.Cells[row++, 1].Value = "DOI: " + context.Article!.Identifier.DOI;
            }

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.PMID))
            {
                sheet.Cells[row++, 1].Value = "PMID: " + context.Article!.Identifier.PMID;
            }

            sheet.Cells[row, 1].Value = "";
        }

        private static void WriteEndpointsSheet(ExcelWorksheet sheet,
                                                IEnumerable<HookM.DataExtractionEndpoint> endpoints,
                                                IReadOnlyDictionary<string, string> populationLookup,
                                                IReadOnlyDictionary<string, string> interventionLookup)
        {
            const int startRow = 7;
            var headers = new[]
            {
                "Name", "Description", "Timepoint", "Measure", "Populations", "Interventions", "Result Summary", "Effect Size", "Notes", "Confirmed"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cells[startRow, i + 1].Value = headers[i];
                sheet.Cells[startRow, i + 1].Style.Font.Bold = true;
                sheet.Cells[startRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[startRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            var row = startRow + 1;
            foreach (var endpoint in endpoints)
            {
                if (endpoint is null)
                {
                    continue;
                }

                sheet.Cells[row, 1].Value = endpoint.Name;
                sheet.Cells[row, 2].Value = endpoint.Description;
                sheet.Cells[row, 3].Value = endpoint.Timepoint;
                sheet.Cells[row, 4].Value = endpoint.Measure;
                sheet.Cells[row, 5].Value = FormatLinked(endpoint.PopulationIds, populationLookup);
                sheet.Cells[row, 6].Value = FormatLinked(endpoint.InterventionIds, interventionLookup);
                sheet.Cells[row, 7].Value = endpoint.ResultSummary;
                sheet.Cells[row, 8].Value = endpoint.EffectSize;
                sheet.Cells[row, 9].Value = endpoint.Notes;
                sheet.Cells[row, 10].Value = endpoint.Confirmed ? "Yes" : "No";
                row++;
            }

            sheet.Cells[startRow, 1, Math.Max(row, startRow + 1), headers.Length].AutoFitColumns();
        }

        private static void WriteBaselinesSheet(ExcelWorksheet sheet, IEnumerable<HookM.DataExtractionTable> tables)
        {
            const int startRow = 7;
            var headers = new[]
            {
                "Title", "Classification", "Pages", "Summary", "Source", "Provenance Hash"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cells[startRow, i + 1].Value = headers[i];
                sheet.Cells[startRow, i + 1].Style.Font.Bold = true;
                sheet.Cells[startRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[startRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            var row = startRow + 1;
            foreach (var table in tables)
            {
                if (table is null)
                {
                    continue;
                }

                var classification = ParseClassification(table.Caption);
                if (classification != TableClassificationKind.Baseline)
                {
                    continue;
                }

                sheet.Cells[row, 1].Value = table.Title;
                sheet.Cells[row, 2].Value = classification.ToString();
                sheet.Cells[row, 3].Value = table.Pages is null || table.Pages.Count == 0 ? "(unspecified)" : string.Join(", ", table.Pages);
                sheet.Cells[row, 4].Value = table.Summary;
                sheet.Cells[row, 5].Value = table.SourcePath;
                sheet.Cells[row, 6].Value = table.ProvenanceHash;
                row++;
            }

            sheet.Cells[startRow, 1, Math.Max(row, startRow + 1), headers.Length].AutoFitColumns();
        }

        private static TableClassificationKind ParseClassification(string? caption)
        {
            if (Enum.TryParse<TableClassificationKind>(caption, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return TableClassificationKind.Unknown;
        }

        private static string FormatLinked(IEnumerable<string> ids, IReadOnlyDictionary<string, string> lookup)
        {
            if (ids is null)
            {
                return string.Empty;
            }

            var names = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => lookup.TryGetValue(id, out var value) ? value : id)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            return names.Length == 0 ? string.Empty : string.Join(", ", names);
        }

        private static IReadOnlyDictionary<string, string> BuildNameLookup<T>(IEnumerable<T> items, Func<T, string> keySelector, Func<T, string> valueSelector)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
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

        private static void EnsureLicense()
        {
            if (s_licenseApplied)
            {
                return;
            }

            lock (s_licenseLock)
            {
                if (s_licenseApplied)
                {
                    return;
                }

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                s_licenseApplied = true;
            }
        }
    }
}
