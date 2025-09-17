using System;
using System.Collections.Generic;
using System.Linq;

namespace LM.Infrastructure.Utils
{
    public static class TagNormalizer
    {
        /// <summary>
        /// Splits a raw keyword string on common separators and trims quotes/smart quotes.
        /// Returns distinct, case-insensitive tags in original casing.
        /// </summary>
        public static IEnumerable<string> SplitAndNormalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            // Normalize curly quotes to straight
            var normalized = raw.Replace('“', '"').Replace('”', '"').Replace('’', '\'');

            var parts = normalized.Split(new[] { ';', ',', '|', '\n', '\r', '\t' },
                                         StringSplitOptions.RemoveEmptyEntries);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in parts)
            {
                var t = p.Trim().Trim('"', '\'', '“', '”', '’');
                if (t.Length == 0) continue;
                if (seen.Add(t)) yield return t;
            }
        }
    }
}
