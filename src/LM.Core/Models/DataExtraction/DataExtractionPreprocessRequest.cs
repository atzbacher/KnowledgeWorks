#nullable enable
using System;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Describes the input bundle provided to the data extraction pre-processor.
    /// </summary>
    public sealed class DataExtractionPreprocessRequest
    {
        public DataExtractionPreprocessRequest(string sourcePdfPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePdfPath))
                throw new ArgumentException("Source PDF path must be provided.", nameof(sourcePdfPath));

            SourcePdfPath = sourcePdfPath;
        }

        /// <summary>The absolute path to the PDF artefact supplied by the user.</summary>
        public string SourcePdfPath { get; }

        /// <summary>Optional absolute path to an XML sibling (e.g. publisher provided structured XML).</summary>
        public string? SourceXmlPath { get; init; }

        /// <summary>
        /// Optional caller supplied cache key. When provided the preprocessor may reuse staged assets instead of recomputing.
        /// </summary>
        public string? PreferredCacheKey { get; init; }
    }
}
