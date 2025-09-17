using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Approximates similarity between two files (later: content-aware).
    /// Phase 1: naive filename token overlap; Phase 2+: text/image-based.
    /// </summary>
    public interface ISimilarityService
    {
        Task<double> ComputeFileSimilarityAsync(string filePathA, string filePathB, CancellationToken ct = default);
    }
}
