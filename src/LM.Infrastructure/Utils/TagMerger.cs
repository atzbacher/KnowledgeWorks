#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LM.Infrastructure.Utils
{
    /// <summary>Small helper to combine existing CSV tags with new keywords.</summary>
    public static class TagMerger
    {
        public static string? Merge(string? existingCsv, IEnumerable<string>? add)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(existingCsv))
            {
                foreach (var t in existingCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    set.Add(t.Trim());
            }

            if (add is not null)
            {
                foreach (var k in add.Where(s => !string.IsNullOrWhiteSpace(s)))
                    set.Add(k!.Trim());
            }

            return set.Count == 0 ? null : string.Join(", ", set);
        }
    }
}
