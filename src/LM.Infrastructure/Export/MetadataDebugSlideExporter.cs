using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using LM.Core.Abstractions;
using LM.Core.Models;

namespace LM.Infrastructure.Export
{
    /// <summary>
    /// Minimal, robust PPTX writer for debugging metadata.
    /// One slide per FileMetadata: title + bullet lines.
    /// </summary>
    public sealed class MetadataDebugSlideExporter : IMetadataDebugSlideExporter
    {
        public Task<string> ExportAsync(IEnumerable<FileMetadata> items, string outPath, CancellationToken ct = default)
        {
            var list = items?.ToList() ?? new List<FileMetadata>();
            if (list.Count == 0) return Task.FromResult(outPath);

            using var doc = PresentationDocument.Create(outPath, PresentationDocumentType.Presentation);
            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new Presentation();

            // Master + layout (no brittle layout 'Type' set)
            var master = presPart.AddNewPart<SlideMasterPart>();
            master.SlideMaster = new SlideMaster(
                new CommonSlideData(new ShapeTree(
                    new NonVisualGroupShapeProperties(
                        new NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()))),
                new SlideLayoutIdList(),
                new TextStyles());

            var layout = master.AddNewPart<SlideLayoutPart>();
            layout.SlideLayout = new SlideLayout(
                new CommonSlideData(new ShapeTree(
                    new NonVisualGroupShapeProperties(
                        new NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()))),
                new ColorMapOverride(new A.MasterColorMapping()));

            master.SlideMaster.SlideLayoutIdList!.Append(new SlideLayoutId
            {
                Id = 1U,
                RelationshipId = master.GetIdOfPart(layout)
            });

            presPart.Presentation.AppendChild(new SlideMasterIdList(
                new SlideMasterId { Id = 1U, RelationshipId = presPart.GetIdOfPart(master) }));
            presPart.Presentation.AppendChild(new SlideIdList());

            // Create slides
            uint sid = 256U;
            foreach (var meta in list)
            {
                ct.ThrowIfCancellationRequested();

                var sp = presPart.AddNewPart<SlidePart>();
                sp.Slide = new Slide(
                    new CommonSlideData(new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = "" },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new A.TransformGroup()))),
                    new ColorMapOverride(new A.MasterColorMapping()));

                sp.AddPart(layout); // link layout

                AddTitle(sp, meta.Title ?? "(untitled)");
                AddBullets(sp, BuildBullets(meta));

                presPart.Presentation.SlideIdList!.Append(new SlideId
                {
                    Id = sid++,
                    RelationshipId = presPart.GetIdOfPart(sp)
                });
            }

            presPart.Presentation.Save();
            return Task.FromResult(outPath);
        }

        private static IEnumerable<string> BuildBullets(FileMetadata m)
        {
            if (m.Authors is { Count: > 0 }) yield return "Authors: " + string.Join(", ", m.Authors);
            if (m.Year is not null)          yield return "Year: " + m.Year;
            if (!string.IsNullOrWhiteSpace(m.Source)) yield return "Source: " + m.Source;
            if (!string.IsNullOrWhiteSpace(m.Doi))    yield return "DOI: " + m.Doi;
            if (!string.IsNullOrWhiteSpace(m.Pmid))   yield return "PMID: " + m.Pmid;
            if (m.Tags is { Count: > 0 })   yield return "Tags: " + string.Join("; ", m.Tags);
        }

        private static void AddTitle(SlidePart sp, string text)
        {
            var tree = sp.Slide.CommonSlideData!.ShapeTree!;
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
                        new A.Run(new A.Text(text ?? "")),
                        new A.EndParagraphRunProperties { Language = "en-US" })));
            tree.Append(shape);
        }

        private static void AddBullets(SlidePart sp, IEnumerable<string> lines)
        {
            var tree = sp.Slide.CommonSlideData!.ShapeTree!;
            var body = new Shape(
                new NonVisualShapeProperties(
                    new NonVisualDrawingProperties { Id = 3U, Name = "Content" },
                    new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Index = 1U })),
                new ShapeProperties(),
                new TextBody(new A.BodyProperties(), new A.ListStyle()));

            var tb = body.TextBody!;
            foreach (var l in lines)
                tb.Append(new A.Paragraph(new A.ParagraphProperties { Level = 0 }, new A.Run(new A.Text(l ?? ""))));

            if (!tb.Elements<A.Paragraph>().Any())
                tb.Append(new A.Paragraph(new A.Run(new A.Text(""))));

            tree.Append(body);
        }
    }
}
