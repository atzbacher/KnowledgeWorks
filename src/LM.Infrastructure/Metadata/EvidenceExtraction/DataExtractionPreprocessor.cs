#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models.DataExtraction;
using LM.Core.Utils;
using LM.Infrastructure.Metadata.EvidenceExtraction.Tables;
using UglyToad.PdfPig;

namespace LM.Infrastructure.Metadata.EvidenceExtraction
{
    public sealed class DataExtractionPreprocessor : IDataExtractionPreprocessor
    {
        private readonly IHasher _hasher;
        private readonly IWorkSpaceService _workspace;

        public DataExtractionPreprocessor(IHasher hasher, IWorkSpaceService workspace)
        {
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<DataExtractionPreprocessResult> PreprocessAsync(DataExtractionPreprocessRequest request, CancellationToken ct = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var pdfPath = request.SourcePdfPath;
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return DataExtractionPreprocessResult.Empty;

            var hash = request.PreferredCacheKey;
            if (string.IsNullOrWhiteSpace(hash))
            {
                hash = await _hasher.ComputeSha256Async(pdfPath, ct).ConfigureAwait(false);
            }

            var stagingRoot = EvidenceStagingLayout.EnsureStagingRoot(_workspace, hash);
            using var document = PdfDocument.Open(pdfPath);

            var sections = SectionExtractor.Extract(document);
            var tables = await ExtractTablesAsync(document, pdfPath, stagingRoot, hash, ct).ConfigureAwait(false);
            var figures = await ExtractFiguresAsync(document, stagingRoot, hash, ct).ConfigureAwait(false);

            var provenance = new EvidenceProvenance
            {
                SourceSha256 = $"sha256-{hash.ToLowerInvariant()}",
                SourceFileName = Path.GetFileName(pdfPath) ?? string.Empty,
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = SystemUser.GetCurrent(),
                AdditionalMetadata = new Dictionary<string, string>
                {
                    ["source_pdf"] = pdfPath,
                    ["staging_root"] = stagingRoot
                }
            };

            return new DataExtractionPreprocessResult
            {
                Sections = sections,
                Tables = tables,
                Figures = figures,
                Provenance = provenance
            };
        }

        private async Task<IReadOnlyList<PreprocessedTable>> ExtractTablesAsync(PdfDocument document,
                                                                                string pdfPath,
                                                                                string stagingRoot,
                                                                                string hash,
                                                                                CancellationToken ct)
        {
            var extractor = new TabulaTableExtractor(new TabulaTableImageWriter());
            var absoluteTablesRoot = Path.Combine(stagingRoot, "tables");
            var tables = await extractor.ExtractAsync(document, pdfPath, absoluteTablesRoot, hash, ct).ConfigureAwait(false);

            var normalized = new List<PreprocessedTable>(tables.Count);
            foreach (var table in tables)
            {
                var normalizedCsv = EvidenceStagingLayout.NormalizeRelative(_workspace, table.CsvRelativePath);
                var normalizedImage = EvidenceStagingLayout.NormalizeRelative(_workspace, table.ImageRelativePath);

                normalized.Add(new PreprocessedTable
                {
                    Id = table.Id,
                    Title = table.Title,
                    FriendlyName = table.FriendlyName,
                    Classification = table.Classification,
                    Columns = table.Columns,
                    Rows = table.Rows,
                    PageNumbers = table.PageNumbers,
                    CsvRelativePath = normalizedCsv,
                    ImageRelativePath = normalizedImage,
                    DetectedPopulations = table.DetectedPopulations,
                    DetectedEndpoints = table.DetectedEndpoints,
                    Tags = table.Tags,
                    Regions = table.Regions.ToArray(),
                    PageLocations = table.PageLocations.ToArray(),
                    ProvenanceHash = table.ProvenanceHash,
                    ImageProvenanceHash = table.ImageProvenanceHash
                });
            }

            return normalized;
        }

        private async Task<IReadOnlyList<PreprocessedFigure>> ExtractFiguresAsync(PdfDocument document, string stagingRoot, string hash, CancellationToken ct)
        {
            var results = new List<PreprocessedFigure>();
            var figuresRoot = Path.Combine(stagingRoot, "figures");
            foreach (var page in document.GetPages())
            {
                var caption = page.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(l => l.Trim())
                                       .FirstOrDefault(l => l.IndexOf("figure", StringComparison.OrdinalIgnoreCase) >= 0);
                if (string.IsNullOrWhiteSpace(caption))
                    continue;

                var figureId = $"fig-{page.Number}";
                var absolute = await FigureThumbnailGenerator.CreatePlaceholderAsync(figuresRoot, figureId, ct).ConfigureAwait(false);
                var normalized = EvidenceStagingLayout.NormalizeRelative(_workspace, absolute);
                var provenance = ComputeProvenance(hash, figureId);

                results.Add(new PreprocessedFigure
                {
                    Id = figureId,
                    Caption = caption,
                    PageNumbers = new[] { page.Number },
                    ThumbnailRelativePath = normalized,
                    ProvenanceHash = provenance
                });
            }

            return results;
        }

        private static string ComputeProvenance(string hash, string identifier)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{hash}:{identifier}"));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
