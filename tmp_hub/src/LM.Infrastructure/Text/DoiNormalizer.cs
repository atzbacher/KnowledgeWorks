#nullable enable
using System;
using System.Text.RegularExpressions;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Text
{
    /// <summary>
    /// Robust DOI extractor / normalizer.
    /// Handles inputs like:
    ///   "doi:10.1056/NEJMoa1514616 copyright"
    ///   "https://doi.org/10.1001/jamacardio.2022.2695publishedonlineau"
    ///   "See DOI 10.1056/nejmoa1816885"
    /// Returns lower-case DOI, e.g., "10.1056/nejmoa1514616".
    /// </summary>
    public class DoiNormalizer : IDoiNormalizer
    {
        // RFC-ish DOI “core” pattern. We’ll post-trim junk tails after the match.
        private static readonly Regex s_findDoi =
            new(@"10\.\d{4,9}/[^\s""<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Known junk words sometimes appended by extractors with no delimiter.
        private static readonly Regex s_tailWords =
            new(@"(copyright|rights?reserved|publishedonline[a-z]*|onlinefirst[a-z]*|aheadofprint[a-z]*|aheadprint[a-z]*|preprint[a-z]*)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // If a long alpha run (>=6) follows a digit at the end, trim it (e.g., "...2695publishedonline").
        private static readonly Regex s_longAlphaTail =
            new(@"(?<=\d)[a-z]{6,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] s_trim =
            { '.', ',', ';', ':', ')', ']', '}', '>', '…', '—', '–' };

        public string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var s = raw.Trim();

            // Strip common prefixes and position at the "10."
            if (s.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
                s = s[4..].Trim();

            var idx10 = s.IndexOf("10.", StringComparison.OrdinalIgnoreCase);
            if (idx10 > 0) s = s[idx10..];

            // Find a DOI-like substring
            var m = s_findDoi.Match(s);
            if (!m.Success) return null;

            var doi = m.Value;

            // Trim punctuation first
            doi = doi.TrimEnd(s_trim);

            // Must have a slash to be a valid DOI
            if (!doi.Contains('/')) return null;

            // Remove known junk tails and overlong alpha runs
            doi = s_tailWords.Replace(doi, "");
            doi = s_longAlphaTail.Replace(doi, "");

            // Final cleanup
            doi = doi.TrimEnd(s_trim);

            return string.IsNullOrWhiteSpace(doi) ? null : doi.ToLowerInvariant();
        }
    }
}
