using System;
using System.Linq;
using LM.Core.Models;

namespace LM.Infrastructure.Utils
{
    public static class BibliographyHelper
    {
        /// <summary>
        /// Builds a short title like: "Smith J., JAMA, 2023".
        /// Falls back gracefully if components are missing.
        /// </summary>
        public static string GenerateShortTitle(string? title, System.Collections.Generic.IEnumerable<string> authors, string? source, int? year)
        {
            var firstAuthor = authors?.FirstOrDefault();
            var authorLabel = string.Empty;
            if (!string.IsNullOrWhiteSpace(firstAuthor))
            {
                var (ln, init) = ParseAuthor(firstAuthor!);
                authorLabel = string.IsNullOrWhiteSpace(init) ? ln : $"{ln} {init}.";
            }

            var src = string.IsNullOrWhiteSpace(source) ? null : AbbrevSource(source!);

            if (!string.IsNullOrWhiteSpace(authorLabel) && src != null && year.HasValue)
                return $"{authorLabel}, {src}, {year.Value}";
            if (!string.IsNullOrWhiteSpace(authorLabel) && year.HasValue)
                return $"{authorLabel}, {year.Value}";
            if (!string.IsNullOrWhiteSpace(authorLabel) && src != null)
                return $"{authorLabel}, {src}";
            if (year.HasValue && src != null)
                return $"{src}, {year.Value}";
            if (!string.IsNullOrWhiteSpace(authorLabel))
                return authorLabel;
            return title ?? "Untitled";
        }

        private static (string last, string initial) ParseAuthor(string raw)
        {
            // Supports "Last, First" or "First Last" - keeps it simple
            var s = raw.Trim();
            if (s.Contains(","))
            {
                var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                var last = parts[0];
                var init = parts.Length > 1 ? parts[1].FirstOrDefault() : default;
                return (Cap(last), init == default ? "" : init.ToString().ToUpperInvariant());
            }
            else
            {
                var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return (Cap(parts[0]), "");
                var last = parts.Last();
                var init = parts.First().FirstOrDefault();
                return (Cap(last), init == default ? "" : init.ToString().ToUpperInvariant());
            }
        }

        private static string AbbrevSource(string source)
        {
            // Extremely simple abbreviation: take uppercase initials of significant words
            var stop = new[] { "of", "the", "and", "for", "in", "on", "to", "a", "an" };
            var parts = source.Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(w => !stop.Contains(w.ToLowerInvariant()));
            var initials = string.Concat(parts.Select(w => char.ToUpperInvariant(w[0])));
            return string.IsNullOrWhiteSpace(initials) ? source : initials;
        }

        private static string Cap(string s) => s.Length <= 1 ? s.ToUpperInvariant() : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }
}
