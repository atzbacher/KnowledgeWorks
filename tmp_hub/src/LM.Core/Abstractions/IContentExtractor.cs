using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Extracts plain text from a file for content-based similarity.
    /// Implementations must be pure (no UI) and safe on unknown formats.
    /// </summary>
    public interface IContentExtractor
    {
        Task<string> ExtractTextAsync(string absolutePath, CancellationToken ct = default);
    }
}
