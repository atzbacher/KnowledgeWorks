using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.Search;

namespace LM.Core.Abstractions.Configuration
{
    /// <summary>Persists and retrieves search history entries.</summary>
    public interface ISearchHistoryStore
    {
        Task<SearchHistoryDocument> LoadAsync(CancellationToken ct = default);
        Task SaveAsync(SearchHistoryDocument document, CancellationToken ct = default);
    }
}
