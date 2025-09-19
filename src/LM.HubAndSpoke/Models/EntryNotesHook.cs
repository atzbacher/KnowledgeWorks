#nullable enable
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>
    /// Structured notes persisted alongside an entry under hooks/notes.json.
    /// Allows callers to store both the rendered summary and the raw user notes.
    /// </summary>
    public sealed class EntryNotesHook
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "2.0";

        [JsonPropertyName("summary")]
        public EntryNotesSummary? Summary { get; init; }

        [JsonIgnore]
        public string? SummaryText => Summary?.GetRenderedText();

        [JsonPropertyName("userNotes")]
        public string? UserNotes { get; init; }

        [JsonPropertyName("updatedUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime UpdatedUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Rich representation of the generated notes summary. Supports both legacy
    /// plain-text payloads and structured metadata for lit search entries.
    /// </summary>
    [JsonConverter(typeof(EntryNotesSummaryConverter))]
    public sealed class EntryNotesSummary
    {
        [JsonPropertyName("rawText")]
        public string? RawText { get; init; }

        [JsonPropertyName("rendered")]
        public string? Rendered { get; init; }

        [JsonPropertyName("litSearch")]
        public LitSearchNoteSummary? LitSearch { get; init; }

        public static EntryNotesSummary FromRawText(string text) => new()
        {
            RawText = text,
            Rendered = text
        };

        public static EntryNotesSummary FromLitSearch(LitSearchNoteSummary summary, string? renderedText) => new()
        {
            RawText = renderedText,
            Rendered = string.IsNullOrWhiteSpace(renderedText)
                ? summary.ToDisplayString()
                : renderedText,
            LitSearch = summary
        };

        public string? GetRenderedText()
        {
            if (!string.IsNullOrWhiteSpace(Rendered))
                return Rendered;

            if (!string.IsNullOrWhiteSpace(RawText))
                return RawText;

            if (LitSearch is not null)
                return LitSearch.ToDisplayString();

            return null;
        }
    }

    /// <summary>
    /// Metadata captured when a lit search entry persists notes.
    /// </summary>
    public sealed class LitSearchNoteSummary
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("query")]
        public string Query { get; init; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; init; } = string.Empty;

        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; init; }

        [JsonPropertyName("createdUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime CreatedUtc { get; init; }

        [JsonPropertyName("runCount")]
        public int RunCount { get; init; }

        [JsonPropertyName("derivedFromEntryId")]
        public string? DerivedFromEntryId { get; init; }

        [JsonPropertyName("latestRun")]
        public LitSearchNoteRunSummary? LatestRun { get; init; }

        public string ToDisplayString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Title))
                sb.AppendLine($"Title: {Title.Trim()}");

            var query = string.IsNullOrWhiteSpace(Query) ? "unknown" : Query.Trim();
            var provider = string.IsNullOrWhiteSpace(Provider) ? "unknown" : Provider.Trim();
            sb.AppendLine($"Query: {query}");
            sb.AppendLine($"Provider: {provider}");

            var createdBy = string.IsNullOrWhiteSpace(CreatedBy) ? "unknown" : CreatedBy.Trim();
            sb.AppendLine($"Created by {createdBy} on {EntryNotesFormatting.FormatTimestamp(CreatedUtc)}.");
            sb.AppendLine($"Run count: {Math.Max(RunCount, 0)}");

            if (LatestRun is not null)
            {
                sb.AppendLine(LatestRun.ToRunLine(createdBy));
                var rangeLine = LatestRun.ToRangeLine();
                if (rangeLine is not null)
                    sb.AppendLine(rangeLine);
            }

            var derivedFrom = string.IsNullOrWhiteSpace(DerivedFromEntryId)
                ? null
                : DerivedFromEntryId.Trim();
            if (!string.IsNullOrWhiteSpace(derivedFrom))
                sb.AppendLine($"Derived from entry {derivedFrom}.");

            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// Snapshot of the most recent search execution for summary purposes.
    /// </summary>
    public sealed class LitSearchNoteRunSummary
    {
        [JsonPropertyName("runId")]
        public string RunId { get; init; } = string.Empty;

        [JsonPropertyName("runUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime RunUtc { get; init; }

        [JsonPropertyName("totalHits")]
        public int TotalHits { get; init; }

        [JsonPropertyName("executedBy")]
        public string? ExecutedBy { get; init; }

        [JsonPropertyName("from")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? From { get; init; }

        [JsonPropertyName("to")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? To { get; init; }

        internal string ToRunLine(string? fallbackExecutor)
        {
            var executor = string.IsNullOrWhiteSpace(ExecutedBy)
                ? (string.IsNullOrWhiteSpace(fallbackExecutor) ? "unknown" : fallbackExecutor!.Trim())
                : ExecutedBy!.Trim();

            var runUtc = EntryNotesFormatting.FormatTimestamp(RunUtc);
            var hits = TotalHits < 0 ? 0 : TotalHits;

            return $"Latest run executed by {executor} on {runUtc} (hits: {hits}).";
        }

        internal string? ToRangeLine()
        {
            if (!From.HasValue && !To.HasValue)
                return null;

            var fromText = From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "–";
            var toText = To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "–";
            return $"Range: {fromText} → {toText}";
        }
    }

    internal static class EntryNotesFormatting
    {
        public static string FormatTimestamp(DateTime timestamp)
        {
            if (timestamp == default)
                return "unknown";

            try
            {
                var normalized = NormalizeUtc(timestamp);
                return normalized.ToString("u", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                var normalized = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
                return normalized.ToString("u", CultureInfo.InvariantCulture);
            }
        }

        private static DateTime NormalizeUtc(DateTime timestamp)
        {
            return timestamp.Kind switch
            {
                DateTimeKind.Utc => timestamp,
                DateTimeKind.Local => timestamp.ToUniversalTime(),
                _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            };
        }
    }

    internal sealed class EntryNotesSummaryConverter : JsonConverter<EntryNotesSummary?>
    {
        public override EntryNotesSummary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : EntryNotesSummary.FromRawText(text);
            }

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected object or string for notes summary payload.");

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            string? rawText = null;
            string? rendered = null;
            LitSearchNoteSummary? litSearch = null;

            if (root.TryGetProperty("rawText", out var rawTextElement) && rawTextElement.ValueKind == JsonValueKind.String)
                rawText = rawTextElement.GetString();

            if (root.TryGetProperty("rendered", out var renderedElement) && renderedElement.ValueKind == JsonValueKind.String)
                rendered = renderedElement.GetString();

            if (root.TryGetProperty("litSearch", out var litElement) && litElement.ValueKind == JsonValueKind.Object)
                litSearch = JsonSerializer.Deserialize<LitSearchNoteSummary>(litElement.GetRawText(), options);

            return new EntryNotesSummary
            {
                RawText = rawText,
                Rendered = rendered,
                LitSearch = litSearch
            };
        }

        public override void Write(Utf8JsonWriter writer, EntryNotesSummary? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            if (!string.IsNullOrWhiteSpace(value.RawText))
                writer.WriteString("rawText", value.RawText);

            if (!string.IsNullOrWhiteSpace(value.Rendered))
                writer.WriteString("rendered", value.Rendered);

            if (value.LitSearch is not null)
            {
                writer.WritePropertyName("litSearch");
                JsonSerializer.Serialize(writer, value.LitSearch, options);
            }

            writer.WriteEndObject();
        }
    }
}
