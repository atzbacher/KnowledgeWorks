#nullable enable
using System;
using System.Collections.Generic;

namespace LM.Infrastructure.Utils
{
    /// <summary>Small helper to combine existing CSV tags with new keywords.</summary>
    public static class TagMerger
    {
        public static string? Merge(string? existingCsv, IEnumerable<string>? add)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddIfNew(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                var trimmed = value.Trim();
                if (trimmed.Length == 0)
                    return;

                if (seen.Add(trimmed))
                    ordered.Add(trimmed);
            }

            if (!string.IsNullOrWhiteSpace(existingCsv))
            {
                foreach (var token in existingCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    AddIfNew(token);
                }
            }

            if (add is not null)
            {
                foreach (var extra in add)
                {
                    AddIfNew(extra);
                }
            }

            return ordered.Count == 0 ? null : string.Join(", ", ordered);
        }
    }
}
