using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LM.App.Wpf.Library.Search
{
    internal readonly record struct LibraryInlineDirectiveResult(
        string MetadataQuery,
        bool HasFullTextDirective,
        string? FullTextQuery,
        bool HasFromDirective,
        DateTime? FromDate,
        bool HasToDirective,
        DateTime? ToDate);

    internal sealed class LibraryInlineDirectiveParser
    {
        private static readonly Regex FullTextRegex = new(
            @"(?ix)\bFULLTEXT:\s*(?:""(?<quoted>(?:\\.|[^""\\])*)""|(?<word>\S+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FromRegex = new(
            @"(?ix)\bFROM:\s*(?:""(?<quoted>(?:\\.|[^""\\])*)""|(?<word>\S+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ToRegex = new(
            @"(?ix)\bTO:\s*(?:""(?<quoted>(?:\\.|[^""\\])*)""|(?<word>\S+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex WhitespaceReducer = new(@"\s+", RegexOptions.Compiled);

        public LibraryInlineDirectiveResult Parse(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Trace.WriteLine("[LibraryInlineDirectiveParser] Query empty; no directives detected.");
                return new LibraryInlineDirectiveResult(string.Empty, false, string.Empty, false, null, false, null);
            }

            var working = query;

            var hasFullText = false;
            string? fullText = null;
            working = FullTextRegex.Replace(working, match =>
            {
                hasFullText = true;
                fullText = ExtractValue(match);
                Trace.WriteLine($"[LibraryInlineDirectiveParser] Extracted FULLTEXT directive → '{fullText}'.");
                return " ";
            });

            var hasFrom = false;
            DateTime? fromDate = null;
            working = FromRegex.Replace(working, match =>
            {
                hasFrom = true;
                var raw = ExtractValue(match);
                if (TryParseDate(raw, isUpperBound: false, out var parsed))
                {
                    fromDate = parsed;
                    Trace.WriteLine($"[LibraryInlineDirectiveParser] Parsed FROM directive '{raw}' → {parsed:yyyy-MM-dd}.");
                }
                else
                {
                    Trace.WriteLine($"[LibraryInlineDirectiveParser] Failed to parse FROM directive '{raw}'.");
                    fromDate = null;
                }

                return " ";
            });

            var hasTo = false;
            DateTime? toDate = null;
            working = ToRegex.Replace(working, match =>
            {
                hasTo = true;
                var raw = ExtractValue(match);
                if (TryParseDate(raw, isUpperBound: true, out var parsed))
                {
                    toDate = parsed;
                    Trace.WriteLine($"[LibraryInlineDirectiveParser] Parsed TO directive '{raw}' → {parsed:yyyy-MM-dd}.");
                }
                else
                {
                    Trace.WriteLine($"[LibraryInlineDirectiveParser] Failed to parse TO directive '{raw}'.");
                    toDate = null;
                }

                return " ";
            });

            var metadataQuery = NormalizeWhitespace(working);
            var normalizedFullText = NormalizeFullText(fullText);

            if (!string.Equals(metadataQuery, query, StringComparison.Ordinal))
            {
                Trace.WriteLine($"[LibraryInlineDirectiveParser] Metadata query normalized to '{metadataQuery}'.");
            }

            return new LibraryInlineDirectiveResult(metadataQuery, hasFullText, normalizedFullText, hasFrom, fromDate, hasTo, toDate);
        }

        private static string ExtractValue(Match match)
        {
            if (match is null)
            {
                return string.Empty;
            }

            var quoted = match.Groups["quoted"];
            if (quoted.Success)
            {
                return NormalizeQuotedValue(quoted.Value);
            }

            var word = match.Groups["word"];
            return word.Success ? word.Value.Trim() : string.Empty;
        }

        private static string NormalizeQuotedValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                return Regex.Unescape(value).Trim();
            }
            catch (ArgumentException ex)
            {
                Trace.WriteLine($"[LibraryInlineDirectiveParser] Failed to unescape quoted value '{value}': {ex.Message}");
                return value.Trim();
            }
        }

        private static string NormalizeFullText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Trim();
        }

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return WhitespaceReducer.Replace(text, " ").Trim();
        }

        private static bool TryParseDate(string value, bool isUpperBound, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            try
            {
                return parts.Length switch
                {
                    3 => ParseDayMonthYear(parts, out result),
                    2 => ParseMonthYear(parts, isUpperBound, out result),
                    1 => ParseYear(parts[0], isUpperBound, out result),
                    _ => false
                };
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or FormatException or OverflowException)
            {
                Trace.WriteLine($"[LibraryInlineDirectiveParser] Date parsing threw for '{value}': {ex.Message}");
                result = default;
                return false;
            }
        }

        private static bool ParseDayMonthYear(IReadOnlyList<string> parts, out DateTime result)
        {
            result = default;
            if (!TryParseComponent(parts[0], out var day) ||
                !TryParseComponent(parts[1], out var month) ||
                !TryParseComponent(parts[2], out var year))
            {
                return false;
            }

            result = new DateTime(year, month, day);
            return true;
        }

        private static bool ParseMonthYear(IReadOnlyList<string> parts, bool isUpperBound, out DateTime result)
        {
            result = default;
            if (!TryParseComponent(parts[0], out var month) ||
                !TryParseComponent(parts[1], out var year))
            {
                return false;
            }

            var day = isUpperBound ? DateTime.DaysInMonth(year, month) : 1;
            result = new DateTime(year, month, day);
            return true;
        }

        private static bool ParseYear(string segment, bool isUpperBound, out DateTime result)
        {
            result = default;
            if (!TryParseComponent(segment, out var year))
            {
                return false;
            }

            var month = isUpperBound ? 12 : 1;
            var day = isUpperBound ? DateTime.DaysInMonth(year, month) : 1;
            result = new DateTime(year, month, day);
            return true;
        }

        private static bool TryParseComponent(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}

