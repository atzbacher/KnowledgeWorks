#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LM.Core.Models.DataExtraction;

namespace LM.Review.Core.DataExtraction
{
    /// <summary>
    /// Provides deterministic regex/dictionary based mappings to auto-label table components.
    /// </summary>
    public static class TableVocabulary
    {
        private static readonly Regex s_baselineRegex = new("\\b(baseline|characteristics|demographic|enrol(l)?ment)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_outcomeRegex = new("\\b(outcome|event|mortality|response|efficacy|adverse)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_timepointRegex = new("\\b(day|week|month|year|follow[- ]?up)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, TableColumnRole> s_columnDictionary = new(StringComparer.OrdinalIgnoreCase)
        {
            ["arm"] = TableColumnRole.Population,
            ["group"] = TableColumnRole.Population,
            ["population"] = TableColumnRole.Population,
            ["treatment"] = TableColumnRole.Intervention,
            ["intervention"] = TableColumnRole.Intervention,
            ["control"] = TableColumnRole.Intervention,
            ["outcome"] = TableColumnRole.Outcome,
            ["endpoint"] = TableColumnRole.Outcome,
            ["measure"] = TableColumnRole.Measure,
            ["time"] = TableColumnRole.Timepoint,
            ["timepoint"] = TableColumnRole.Timepoint,
            ["visit"] = TableColumnRole.Timepoint,
            ["value"] = TableColumnRole.Value,
            ["mean"] = TableColumnRole.Value,
            ["median"] = TableColumnRole.Value,
            ["sd"] = TableColumnRole.Value,
            ["n"] = TableColumnRole.Value
        };

        private static readonly HashSet<string> s_populationKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "arm",
            "group",
            "cohort",
            "population",
            "baseline",
            "placebo",
            "treatment",
            "control"
        };

        private static readonly HashSet<string> s_endpointKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "mortality",
            "response",
            "event",
            "efficacy",
            "relapse",
            "remission",
            "progression",
            "adverse"
        };

        public static TableClassificationKind ClassifyTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return TableClassificationKind.Unknown;

            if (s_baselineRegex.IsMatch(title))
                return TableClassificationKind.Baseline;

            if (s_outcomeRegex.IsMatch(title))
                return TableClassificationKind.Outcome;

            return TableClassificationKind.Unknown;
        }

        public static TableRowRole ClassifyRowLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return TableRowRole.Unknown;

            if (s_baselineRegex.IsMatch(label))
                return TableRowRole.Baseline;

            if (s_outcomeRegex.IsMatch(label))
                return TableRowRole.Outcome;

            if (label.Trim().EndsWith(":", StringComparison.Ordinal))
                return TableRowRole.Header;

            return TableRowRole.Unknown;
        }

        public static TableColumnRole ClassifyColumnHeader(string? header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return TableColumnRole.Unknown;

            var normalized = header.Trim();
            if (s_columnDictionary.TryGetValue(normalized, out var match))
                return match;

            foreach (var kvp in s_columnDictionary)
            {
                if (normalized.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            if (s_timepointRegex.IsMatch(normalized))
                return TableColumnRole.Timepoint;

            return TableColumnRole.Unknown;
        }

        public static string NormalizeHeader(string header)
            => header?.Trim().ToLowerInvariant() ?? string.Empty;

        public static string? TryDetectPopulation(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var trimmed = token.Trim();
            if (s_populationKeywords.Contains(trimmed))
                return trimmed;

            return s_populationKeywords.FirstOrDefault(k => trimmed.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        public static string? TryDetectEndpoint(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var trimmed = token.Trim();
            if (s_endpointKeywords.Contains(trimmed))
                return trimmed;

            return s_endpointKeywords.FirstOrDefault(k => trimmed.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
    }
}
