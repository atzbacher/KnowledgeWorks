#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using LM.Core.Abstractions;
using LM.Infrastructure.Export;
using LM.Infrastructure.FileSystem;
using OfficeOpenXml;
using Xunit;
using HookM = LM.HubSpoke.Models;
using LM.Core.Models.DataExtraction;

namespace LM.Infrastructure.Tests.Export
{
    public sealed class DataExtractionExporterTests
    {
        [Fact]
        public async Task Exporters_ProduceArtifactsWithProvenance()
        {
            using var temp = new TempWorkspace();
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.RootPath);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var entryId = "entry-001";
            PrepareWorkspace(temp.RootPath, entryId);

            var loader = new DataExtractionExportLoader(workspace);
            var pptExporter = new DataExtractionPowerPointExporter(loader);
            var wordExporter = new DataExtractionWordExporter(loader);
            var excelExporter = new DataExtractionExcelExporter(loader);

            Assert.True(await pptExporter.CanExportAsync(entryId));
            Assert.True(await wordExporter.CanExportAsync(entryId));
            Assert.True(await excelExporter.CanExportAsync(entryId));

            var pptPath = Path.Combine(temp.RootPath, "extraction.pptx");
            var docPath = Path.Combine(temp.RootPath, "tables.docx");
            var xlsxPath = Path.Combine(temp.RootPath, "summary.xlsx");

            await pptExporter.ExportAsync(entryId, pptPath);
            await wordExporter.ExportAsync(entryId, docPath);
            await excelExporter.ExportAsync(entryId, xlsxPath);

            Assert.True(File.Exists(pptPath));
            Assert.True(File.Exists(docPath));
            Assert.True(File.Exists(xlsxPath));

            using (var presentation = PresentationDocument.Open(pptPath, false))
            {
                var slideTexts = presentation.PresentationPart!
                    .SlideParts
                    .SelectMany(part => part.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
                    .Select(t => t.Text)
                    .ToList();

                Assert.Contains(slideTexts, text => text.Contains("Provenance hash", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(slideTexts, text => text.Contains("DOI:", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(slideTexts, text => text.Contains("Pages:", StringComparison.OrdinalIgnoreCase));
            }

            using (var document = WordprocessingDocument.Open(docPath, false))
            {
                var innerText = document.MainDocumentPart!.Document.Body!.InnerText;
                Assert.Contains("Provenance hash", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Baseline", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Placebo", innerText, StringComparison.OrdinalIgnoreCase);
            }

            using (var package = new ExcelPackage(new FileInfo(xlsxPath)))
            {
                var endpointsSheet = package.Workbook.Worksheets["Endpoints"];
                var baselinesSheet = package.Workbook.Worksheets["Baselines"];

                Assert.NotNull(endpointsSheet);
                Assert.NotNull(baselinesSheet);

                Assert.Equal("Respiratory Rate", endpointsSheet!.Cells[8, 1].GetValue<string>());
                Assert.Contains("Group A", endpointsSheet.Cells[8, 5].GetValue<string>());
                Assert.Equal("sha256-table-hash", baselinesSheet!.Cells[8, 6].GetValue<string>());
            }
        }

        private static void PrepareWorkspace(string rootPath, string entryId)
        {
            var extractionRelative = "extraction/de/ad/sha256-deadbeef.json";
            var extractionAbsolute = Path.Combine(rootPath, extractionRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(extractionAbsolute)!);

            var tableCsvRelative = "library/baseline.csv";
            var tableCsvAbsolute = Path.Combine(rootPath, tableCsvRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(tableCsvAbsolute)!);
            File.WriteAllLines(tableCsvAbsolute, new[]
            {
                "Group,Value",
                "Placebo,12",
                "Treatment,18"
            });

            var hook = new HookM.DataExtractionHook
            {
                ExtractedBy = "tester",
                ExtractedAtUtc = DateTime.UtcNow,
                Populations = new List<HookM.DataExtractionPopulation>
                {
                    new() { Id = "pop-1", Label = "Group A" }
                },
                Interventions = new List<HookM.DataExtractionIntervention>
                {
                    new() { Id = "int-1", Name = "Drug" }
                },
                Endpoints = new List<HookM.DataExtractionEndpoint>
                {
                    new()
                    {
                        Id = "ep-1",
                        Name = "Respiratory Rate",
                        PopulationIds = new List<string> { "pop-1" },
                        InterventionIds = new List<string> { "int-1" },
                        ResultSummary = "Improved"
                    }
                },
                Tables = new List<HookM.DataExtractionTable>
                {
                    new()
                    {
                        Id = "table-1",
                        Title = "Baseline",
                        Caption = TableClassificationKind.Baseline.ToString(),
                        SourcePath = tableCsvRelative,
                        ProvenanceHash = "sha256-table-hash",
                        Pages = new List<string> { "3" }
                    }
                },
                Figures = new List<HookM.DataExtractionFigure>
                {
                    new()
                    {
                        Id = "fig-1",
                        Title = "Figure 1",
                        Caption = "Outcome",
                        Pages = new List<string> { "5" },
                        ProvenanceHash = "sha256-figure-hash"
                    }
                }
            };

            var extractionJson = JsonSerializer.Serialize(hook, HookM.JsonStd.Options);
            File.WriteAllText(extractionAbsolute, extractionJson);

            var entryDir = Path.Combine(rootPath, "entries", entryId);
            Directory.CreateDirectory(Path.Combine(entryDir, "hooks"));

            var hub = new HookM.EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Sample Entry",
                Hooks = new HookM.EntryHooks
                {
                    Article = "entries/" + entryId + "/hooks/article.json",
                    DataExtraction = extractionRelative.Replace(Path.DirectorySeparatorChar, '/'),
                }
            };

            var hubPath = Path.Combine(entryDir, "hub.json");
            File.WriteAllText(hubPath, JsonSerializer.Serialize(hub, HookM.JsonStd.Options));

            var article = new HookM.ArticleHook
            {
                Identifier = new HookM.ArticleIdentifier
                {
                    DOI = "10.1000/sample",
                    PMID = "123456"
                }
            };

            var articlePath = Path.Combine(entryDir, "hooks", "article.json");
            File.WriteAllText(articlePath, JsonSerializer.Serialize(article, HookM.JsonStd.Options));
        }

        private sealed class TempWorkspace : IDisposable
        {
            public string RootPath { get; }

            public TempWorkspace()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "kw-export-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootPath))
                    {
                        Directory.Delete(RootPath, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
