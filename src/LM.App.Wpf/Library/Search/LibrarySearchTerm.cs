using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LM.Core.Models;

namespace LM.App.Wpf.Library.Search
{
    internal enum LibrarySearchTermOperation
    {
        Contains,
        Equals,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Between,
        InSet
    }

    internal sealed class LibrarySearchTerm
    {
        private LibrarySearchTerm(LibrarySearchField field)
        {
            Field = field;
            Operation = LibrarySearchTermOperation.Contains;
            Value = string.Empty;
        }

        public LibrarySearchField Field { get; }
        public LibrarySearchTermOperation Operation { get; private set; }
        public string Value { get; private set; }
        public string? SecondaryValue { get; private set; }
        public IReadOnlyList<EntryType>? TypeValues { get; private set; }
        public IReadOnlyList<string>? TextValues { get; private set; }
        public int? NumberValue { get; private set; }
        public int? SecondaryNumberValue { get; private set; }
        public DateTime? DateValue { get; private set; }
        public DateTime? SecondaryDateValue { get; private set; }
        public bool? BooleanValue { get; private set; }

        public static LibrarySearchTerm? Create(string? fieldToken, string? rawValue)
        {
            var field = LibrarySearchFieldMap.Resolve(fieldToken);
            var term = new LibrarySearchTerm(field);

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (field == LibrarySearchField.Any)
                {
                    return null;
                }

                term.Value = string.Empty;
                return term;
            }

            var trimmed = rawValue.Trim();
            term.Value = trimmed;

            switch (field)
            {
                case LibrarySearchField.Type:
                    if (!TryParseTypes(trimmed, out var types))
                    {
                        return null;
                    }

                    term.TypeValues = types;
                    term.Operation = LibrarySearchTermOperation.InSet;
                    break;

                case LibrarySearchField.Year:
                    if (!TryParseIntegerRange(trimmed, out var intResult))
                    {
                        return null;
                    }

                    term.Operation = intResult.Operation;
                    term.NumberValue = intResult.First;
                    term.SecondaryNumberValue = intResult.Second;
                    break;

                case LibrarySearchField.AddedOn:
                    if (!TryParseDateRange(trimmed, out var dateResult))
                    {
                        return null;
                    }

                    term.Operation = dateResult.Operation;
                    term.DateValue = dateResult.First;
                    term.SecondaryDateValue = dateResult.Second;
                    break;

                case LibrarySearchField.Internal:
                    if (!TryParseBoolean(trimmed, out var flag))
                    {
                        return null;
                    }

                    term.Operation = LibrarySearchTermOperation.Equals;
                    term.BooleanValue = flag;
                    break;

                case LibrarySearchField.Tags:
                    term.TextValues = SplitValues(trimmed);
                    term.Operation = LibrarySearchTermOperation.InSet;
                    break;

                default:
                    term.Operation = LibrarySearchTermOperation.Contains;
                    break;
            }

            return term;
        }

        private static bool TryParseTypes(string text, out IReadOnlyList<EntryType> values)
        {
            var segments = text.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => segment.Length > 0)
                .ToArray();

            if (segments.Length == 0)
            {
                values = Array.Empty<EntryType>();
                return false;
            }

            var parsed = new List<EntryType>();
            foreach (var segment in segments)
            {
                if (Enum.TryParse<EntryType>(segment, ignoreCase: true, out var type))
                {
                    parsed.Add(type);
                    continue;
                }

                switch (segment.ToLowerInvariant())
                {
                    case "publication":
                    case "paper":
                        parsed.Add(EntryType.Publication);
                        break;
                    case "presentation":
                    case "talk":
                        parsed.Add(EntryType.Presentation);
                        break;
                    case "whitepaper":
                    case "white-paper":
                        parsed.Add(EntryType.WhitePaper);
                        break;
                    case "slidedeck":
                    case "slides":
                    case "deck":
                        parsed.Add(EntryType.SlideDeck);
                        break;
                    case "report":
                        parsed.Add(EntryType.Report);
                        break;
                    case "other":
                        parsed.Add(EntryType.Other);
                        break;
                    case "litsearch":
                    case "lit-search":
                        parsed.Add(EntryType.LitSearch);
                        break;
                }
            }

            values = parsed.Distinct().ToArray();
            return values.Count > 0;
        }

        private static bool TryParseIntegerRange(string text, out (LibrarySearchTermOperation Operation, int? First, int? Second) result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (text.Contains("..", StringComparison.Ordinal))
            {
                var parts = text.Split("..", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var to))
                {
                    result = (LibrarySearchTermOperation.Between, from, to);
                    return true;
                }
                return false;
            }

            if (text.Contains('-') && !text.StartsWith("-", StringComparison.Ordinal))
            {
                var parts = text.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromDash) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var toDash))
                {
                    result = (LibrarySearchTermOperation.Between, fromDash, toDash);
                    return true;
                }
            }

            var comparison = GetComparisonPrefix(text, out var remainder);
            if (!int.TryParse(remainder, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            result = comparison switch
            {
                ">" => (LibrarySearchTermOperation.GreaterThan, value, null),
                ">=" => (LibrarySearchTermOperation.GreaterThanOrEqual, value, null),
                "<" => (LibrarySearchTermOperation.LessThan, value, null),
                "<=" => (LibrarySearchTermOperation.LessThanOrEqual, value, null),
                _ => (LibrarySearchTermOperation.Equals, value, null)
            };
            return true;
        }

        private static bool TryParseDateRange(string text, out (LibrarySearchTermOperation Operation, DateTime? First, DateTime? Second) result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (text.Contains("..", StringComparison.Ordinal))
            {
                var parts = text.Split("..", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && TryParseDate(parts[0], out var from) && TryParseDate(parts[1], out var to))
                {
                    result = (LibrarySearchTermOperation.Between, from, to);
                    return true;
                }
                return false;
            }

            if (text.Contains('-') && CountChar(text, '-') == 2)
            {
                // If it's YYYY-MM-DD.. treat as a single date. For ranges we rely on "..".
            }
            else if (text.Contains('-', StringComparison.Ordinal) && !text.StartsWith("-", StringComparison.Ordinal))
            {
                var parts = text.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && TryParseDate(parts[0], out var fromDash) && TryParseDate(parts[1], out var toDash))
                {
                    result = (LibrarySearchTermOperation.Between, fromDash, toDash);
                    return true;
                }
            }

            var comparison = GetComparisonPrefix(text, out var remainder);
            if (!TryParseDate(remainder, out var parsed))
            {
                return false;
            }

            result = comparison switch
            {
                ">" => (LibrarySearchTermOperation.GreaterThan, parsed, null),
                ">=" => (LibrarySearchTermOperation.GreaterThanOrEqual, parsed, null),
                "<" => (LibrarySearchTermOperation.LessThan, parsed, null),
                "<=" => (LibrarySearchTermOperation.LessThanOrEqual, parsed, null),
                _ => (LibrarySearchTermOperation.Equals, parsed, null)
            };
            return true;
        }

        private static bool TryParseBoolean(string text, out bool value)
        {
            text = text.Trim();
            switch (text.ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "y":
                case "1":
                case "internal":
                case "in":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "n":
                case "0":
                case "external":
                case "out":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static IReadOnlyList<string> SplitValues(string text)
        {
            return text.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToArray();
        }

        private static string GetComparisonPrefix(string text, out string remainder)
        {
            if (text.StartsWith(">=", StringComparison.Ordinal))
            {
                remainder = text[2..].Trim();
                return ">=";
            }

            if (text.StartsWith("<=", StringComparison.Ordinal))
            {
                remainder = text[2..].Trim();
                return "<=";
            }

            if (text.StartsWith(">", StringComparison.Ordinal))
            {
                remainder = text[1..].Trim();
                return ">";
            }

            if (text.StartsWith("<", StringComparison.Ordinal))
            {
                remainder = text[1..].Trim();
                return "<";
            }

            remainder = text.Trim();
            return string.Empty;
        }

        private static bool TryParseDate(string text, out DateTime value)
        {
            text = text.Trim();
            if (DateTime.TryParseExact(text, new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd" }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
            {
                return true;
            }

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out value))
            {
                return true;
            }

            return false;
        }

        private static int CountChar(string text, char c)
        {
            var count = 0;
            foreach (var ch in text)
            {
                if (ch == c)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
