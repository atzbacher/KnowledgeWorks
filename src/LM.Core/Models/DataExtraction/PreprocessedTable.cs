#nullable enable
using System;
using System.Collections.Generic;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// CSV backed table generated during pre-processing.
    /// </summary>
    public sealed class PreprocessedTable
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Title { get; init; } = string.Empty;
        public TableClassificationKind Classification { get; init; } = TableClassificationKind.Unknown;
        public IReadOnlyList<TableColumnMapping> Columns { get; init; } = new List<TableColumnMapping>();
        public IReadOnlyList<TableRowMapping> Rows { get; init; } = new List<TableRowMapping>();
        public IReadOnlyList<int> PageNumbers { get; init; } = new List<int>();
        public string CsvRelativePath { get; init; } = string.Empty;
        public string ImageRelativePath { get; init; } = string.Empty;
        public IReadOnlyList<string> DetectedPopulations { get; init; } = new List<string>();
        public IReadOnlyList<string> DetectedEndpoints { get; init; } = new List<string>();
        public IReadOnlyList<string> Tags { get; init; } = new List<string>();
        public string FriendlyName { get; init; } = string.Empty;
        public IReadOnlyList<TableRegion> Regions { get; init; } = new List<TableRegion>();
        public IReadOnlyList<TablePageLocation> PageLocations { get; init; } = new List<TablePageLocation>();
        public string ProvenanceHash { get; init; } = string.Empty;
        public string ImageProvenanceHash { get; init; } = string.Empty;
    }
}
