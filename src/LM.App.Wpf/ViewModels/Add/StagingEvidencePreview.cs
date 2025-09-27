#nullable enable
using System;
using System.Collections.Generic;
using LM.Core.Models.DataExtraction;

namespace LM.App.Wpf.ViewModels
{
    public sealed class StagingEvidencePreview
    {
        public IReadOnlyList<SectionPreview> Sections { get; init; } = Array.Empty<SectionPreview>();
        public IReadOnlyList<TablePreview> Tables { get; init; } = Array.Empty<TablePreview>();
        public IReadOnlyList<FigurePreview> Figures { get; init; } = Array.Empty<FigurePreview>();
        public EvidenceProvenance Provenance { get; init; } = new EvidenceProvenance();

        public sealed class SectionPreview
        {
            public string Heading { get; init; } = string.Empty;
            public string Body { get; init; } = string.Empty;
            public IReadOnlyList<int> Pages { get; init; } = Array.Empty<int>();
        }

        public sealed class TablePreview
        {
            public string Title { get; init; } = string.Empty;
            public TableClassificationKind Classification { get; init; } = TableClassificationKind.Unknown;
            public IReadOnlyList<string> Populations { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> Endpoints { get; init; } = Array.Empty<string>();
            public IReadOnlyList<int> Pages { get; init; } = Array.Empty<int>();
            public IReadOnlyList<TableRegion> Regions { get; init; } = Array.Empty<TableRegion>();
        }

        public sealed class FigurePreview
        {
            public string Caption { get; init; } = string.Empty;
            public IReadOnlyList<int> Pages { get; init; } = Array.Empty<int>();
            public string ThumbnailPath { get; init; } = string.Empty;
        }
    }
}
