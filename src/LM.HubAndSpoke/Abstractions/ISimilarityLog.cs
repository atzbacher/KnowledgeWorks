#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace LM.HubSpoke.Abstractions
{
    public interface ISimilarityLog
    {
        string NewSessionId();

        Task LogAsync(string sessionId,
                      string stagedPath,
                      string candidateEntryId,
                      double score,
                      string method,
                      CancellationToken ct = default);
    }
}
