using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.Search;

namespace LM.Core.Abstractions.Search
{
    /// <summary>
    /// Coordinates running external literature searches and normalizing their results.
    /// </summary>
    public interface ISearchExecutionService
    {
        Task<SearchExecutionResult> ExecuteAsync(SearchExecutionRequest request, CancellationToken ct = default);
    }
}
