using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Extracts structured metadata (title, authors, year, ids) from a file.
    /// Best-effort; returns empty strings/nulls when undecidable.
    /// </summary>
    public interface IMetadataExtractor
    {
        Task<FileMetadata> ExtractAsync(string absolutePath, CancellationToken ct = default);
    }
}
