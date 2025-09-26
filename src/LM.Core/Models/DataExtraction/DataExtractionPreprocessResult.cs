#nullable enable
using System.Collections.Generic;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Structured output returned by the extraction pre-processor.
    /// </summary>
    public sealed class DataExtractionPreprocessResult
    {
        public static DataExtractionPreprocessResult Empty { get; } = new();

        public IReadOnlyList<StructuredSection> Sections { get; init; } = new List<StructuredSection>();
        public IReadOnlyList<PreprocessedTable> Tables { get; init; } = new List<PreprocessedTable>();
        public IReadOnlyList<PreprocessedFigure> Figures { get; init; } = new List<PreprocessedFigure>();
        public EvidenceProvenance Provenance { get; init; } = new EvidenceProvenance();

        public bool IsEmpty => (Sections.Count == 0) && (Tables.Count == 0) && (Figures.Count == 0);

        public DataExtractionPreprocessResult WithProvenance(EvidenceProvenance provenance)
            => new()
            {
                Sections = Sections,
                Tables = Tables,
                Figures = Figures,
                Provenance = provenance
            };
    }
}
