#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;
using A = DocumentFormat.OpenXml.Drawing;

namespace LM.Infrastructure.Export
{
    public sealed class DataExtractionPowerPointExporter : IDataExtractionPowerPointExporter
    {
        private readonly DataExtractionExportLoader _loader;

        public DataExtractionPowerPointExporter(DataExtractionExportLoader loader)
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

            using var document = PresentationDocument.Create(outputPath, PresentationDocumentType.Presentation);
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new Presentation();

            var (masterPart, layoutPart) = CreateSlideMaster(presentationPart);

            AppendTitleSlide(presentationPart, layoutPart, context);

            var endpointLookup = BuildNameLookup(context.Extraction.Endpoints, e => e.Id, e => e.Name);
            var interventionLookup = BuildNameLookup(context.Extraction.Interventions, i => i.Id, i => i.Name);

            foreach (var table in context.Extraction.Tables)
            {
                ct.ThrowIfCancellationRequested();
                AppendArtifactSlide(presentationPart, layoutPart, context, table.Title, BuildTableLines(context, table, endpointLookup, interventionLookup));
            }

            foreach (var figure in context.Extraction.Figures)
            {
                ct.ThrowIfCancellationRequested();
                AppendArtifactSlide(presentationPart, layoutPart, context, figure.Title, BuildFigureLines(context, figure, endpointLookup, interventionLookup));
            }

            presentationPart.Presentation.Save();
            return outputPath;
        }

        private static (SlideMasterPart Master, SlideLayoutPart Layout) CreateSlideMaster(PresentationPart presentationPart)
        {
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
            masterPart.SlideMaster = new SlideMaster(
                new CommonSlideData(new ShapeTree(
                    new NonVisualGroupShapeProperties(
                        new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()))),
                new SlideLayoutIdList(),
                new TextStyles());

            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutPart.SlideLayout = new SlideLayout(
                new CommonSlideData(new ShapeTree(
                    new NonVisualGroupShapeProperties(
                        new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()))),
                new ColorMapOverride(new A.MasterColorMapping()));

            masterPart.SlideMaster.SlideLayoutIdList!.Append(new SlideLayoutId
            {
                Id = 1U,
                RelationshipId = masterPart.GetIdOfPart(layoutPart)
            });

            presentationPart.Presentation.AppendChild(new SlideMasterIdList(
                new SlideMasterId { Id = 1U, RelationshipId = presentationPart.GetIdOfPart(masterPart) }));
            presentationPart.Presentation.AppendChild(new SlideIdList());

            return (masterPart, layoutPart);
        }

        private static void AppendTitleSlide(PresentationPart presentationPart,
                                              SlideLayoutPart layoutPart,
                                              DataExtractionExportContext context)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(
                new CommonSlideData(new ShapeTree(
                    new NonVisualGroupShapeProperties(
                        new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()))),
                new ColorMapOverride(new A.MasterColorMapping()));

            slidePart.AddPart(layoutPart);

            var title = string.IsNullOrWhiteSpace(context.Hub.DisplayTitle)
                ? context.EntryId
                : context.Hub.DisplayTitle;

            AddTitleShape(slidePart, title);

            var lines = new List<string>
            {
                $"Entry ID: {context.EntryId}",
                $"Extracted by {context.Extraction.ExtractedBy} on {context.Extraction.ExtractedAtUtc:yyyy-MM-dd HH:mm} UTC"
            };

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.DOI))
            {
                lines.Add("DOI: " + context.Article!.Identifier.DOI);
            }

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.PMID))
            {
                lines.Add("PMID: " + context.Article!.Identifier.PMID);
            }

            if (!string.IsNullOrWhiteSpace(context.Extraction.Notes))
            {
                lines.Add("Notes: " + context.Extraction.Notes);
            }

            AddBodyShape(slidePart, lines);

            AppendSlideReference(presentationPart, slidePart);
        }

        private static void AppendArtifactSlide(PresentationPart presentationPart,
                                                 SlideLayoutPart layoutPart,
                                                 DataExtractionExportContext context,
                                                 string? title,
                                                 IEnumerable<string> bulletLines)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(
                new CommonSlideData(new ShapeTree(
                    new NonVisualGroupShapeProperties(
                        new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()))),
                new ColorMapOverride(new A.MasterColorMapping()));

            slidePart.AddPart(layoutPart);
            AddTitleShape(slidePart, title ?? "(untitled)");
            AddBodyShape(slidePart, bulletLines);
            AppendSlideReference(presentationPart, slidePart);
        }

        private static void AppendSlideReference(PresentationPart presentationPart, SlidePart slidePart)
        {
            var list = presentationPart.Presentation.SlideIdList ?? presentationPart.Presentation.AppendChild(new SlideIdList());
            var nextId = list.ChildElements.Count == 0
                ? 256U
                : list.ChildElements.Select(e => ((SlideId)e).Id!.Value).Max() + 1U;

            list.Append(new SlideId
            {
                Id = nextId,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            });
        }

        private static void AddTitleShape(SlidePart slidePart, string text)
        {
            var tree = slidePart.Slide.CommonSlideData!.ShapeTree!;
            var shape = new Shape(
                new NonVisualShapeProperties(
                    new NonVisualDrawingProperties { Id = 2U, Name = "Title" },
                    new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Title })),
                new ShapeProperties(),
                new TextBody(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(new A.Text(text ?? string.Empty)),
                        new A.EndParagraphRunProperties { Language = "en-US" })));
            tree.Append(shape);
        }

        private static void AddBodyShape(SlidePart slidePart, IEnumerable<string> lines)
        {
            var tree = slidePart.Slide.CommonSlideData!.ShapeTree!;
            var body = new Shape(
                new NonVisualShapeProperties(
                    new NonVisualDrawingProperties { Id = 3U, Name = "Content" },
                    new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Index = 1U })),
                new ShapeProperties(),
                new TextBody(new A.BodyProperties(), new A.ListStyle()));

            var textBody = body.TextBody!;
            var any = false;
            foreach (var line in lines)
            {
                any = true;
                textBody.Append(new A.Paragraph(new A.ParagraphProperties { Level = 0 }, new A.Run(new A.Text(line ?? string.Empty))));
            }

            if (!any)
            {
                textBody.Append(new A.Paragraph(new A.Run(new A.Text(string.Empty))));
            }

            tree.Append(body);
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

        private static IEnumerable<string> BuildTableLines(DataExtractionExportContext context,
                                                            HookM.DataExtractionTable table,
                                                            IReadOnlyDictionary<string, string> endpointLookup,
                                                            IReadOnlyDictionary<string, string> interventionLookup)
        {
            yield return "Type: Table";

            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                yield return "Caption: " + table.Caption;
            }

            if (!string.IsNullOrWhiteSpace(table.TableLabel))
            {
                yield return "Label: " + table.TableLabel;
            }

            if (!string.IsNullOrWhiteSpace(table.Summary))
            {
                yield return "Summary: " + table.Summary;
            }

            yield return FormatPages(table.Pages);
            yield return FormatLinked("Endpoints", table.LinkedEndpointIds, endpointLookup);
            yield return FormatLinked("Interventions", table.LinkedInterventionIds, interventionLookup);

            if (!string.IsNullOrWhiteSpace(table.SourcePath))
            {
                yield return "Source CSV: " + table.SourcePath;
            }

            yield return "Provenance hash: " + (string.IsNullOrWhiteSpace(table.ProvenanceHash) ? "(missing)" : table.ProvenanceHash);
            yield return BuildReferenceLine(context);
        }

        private static IEnumerable<string> BuildFigureLines(DataExtractionExportContext context,
                                                             HookM.DataExtractionFigure figure,
                                                             IReadOnlyDictionary<string, string> endpointLookup,
                                                             IReadOnlyDictionary<string, string> interventionLookup)
        {
            yield return "Type: Figure";

            if (!string.IsNullOrWhiteSpace(figure.Caption))
            {
                yield return "Caption: " + figure.Caption;
            }

            yield return FormatPages(figure.Pages);
            yield return FormatLinked("Endpoints", figure.LinkedEndpointIds, endpointLookup);
            yield return FormatLinked("Interventions", figure.LinkedInterventionIds, interventionLookup);

            if (!string.IsNullOrWhiteSpace(figure.SourcePath))
            {
                yield return "Source asset: " + figure.SourcePath;
            }

            yield return "Provenance hash: " + (string.IsNullOrWhiteSpace(figure.ProvenanceHash) ? "(missing)" : figure.ProvenanceHash);
            yield return BuildReferenceLine(context);
        }

        private static string BuildReferenceLine(DataExtractionExportContext context)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.DOI))
            {
                parts.Add("DOI: " + context.Article!.Identifier.DOI);
            }

            if (!string.IsNullOrWhiteSpace(context.Article?.Identifier?.PMID))
            {
                parts.Add("PMID: " + context.Article!.Identifier.PMID);
            }

            parts.Add(string.Create(CultureInfo.InvariantCulture, $"Extracted {context.Extraction.ExtractedAtUtc:yyyy-MM-dd}"));
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

        private static string FormatLinked(string label, IEnumerable<string> ids, IReadOnlyDictionary<string, string> lookup)
        {
            if (ids is null)
            {
                return label + ": (none)";
            }

            var names = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => lookup.TryGetValue(id, out var value) ? value : id)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            if (names.Length == 0)
            {
                return label + ": (none)";
            }

            return label + ": " + string.Join(", ", names);
        }
    }
}
