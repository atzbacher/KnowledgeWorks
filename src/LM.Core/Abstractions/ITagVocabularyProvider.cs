using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    public interface ITagVocabularyProvider
    {
        Task<IReadOnlyList<string>> GetAllTagsAsync(CancellationToken ct = default);
    }
}
