using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;
using LM.Core.Models.Search;

namespace LM.Core.Abstractions.Search
{
    /// <summary>
    /// A concrete source that can execute a search against a specific database.
    /// </summary>
    public interface ISearchProvider
    {
        SearchDatabase Database { get; }

        Task<IReadOnlyList<SearchHit>> SearchAsync(string query, DateTime? from, DateTime? to, CancellationToken ct = default);
    }
}
