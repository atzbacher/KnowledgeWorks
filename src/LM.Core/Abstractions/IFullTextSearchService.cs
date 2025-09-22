using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.Search;

namespace LM.Core.Abstractions
{
    public interface IFullTextSearchService
    {
        Task<IReadOnlyList<FullTextSearchHit>> SearchAsync(FullTextSearchQuery query, CancellationToken ct = default);
    }
}
