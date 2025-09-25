using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Entries
{
    public sealed class EntryTagVocabularyProvider : ITagVocabularyProvider
    {
        private readonly IEntryStore _store;

        public EntryTagVocabularyProvider(IEntryStore store)
        {
            _store = store ?? throw new System.ArgumentNullException(nameof(store));
        }

        public async Task<IReadOnlyList<string>> GetAllTagsAsync(CancellationToken ct = default)
        {
            var tags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            await foreach (var entry in _store.EnumerateAsync(ct).ConfigureAwait(false))
            {
                if (entry.Tags is null)
                {
                    continue;
                }

                foreach (var tag in entry.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    var trimmed = tag.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    tags.Add(trimmed);
                }
            }

            return tags
                .OrderBy(static t => t, System.StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
