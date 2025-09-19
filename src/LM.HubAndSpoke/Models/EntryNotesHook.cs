#nullable enable
using System;
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
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("userNotes")]
        public string? UserNotes { get; init; }

        [JsonPropertyName("updatedUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime UpdatedUtc { get; init; } = DateTime.UtcNow;
    }
}
