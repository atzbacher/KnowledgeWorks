#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LM.Core.Models;

namespace LM.HubSpoke.Models
{
    public sealed class EntryChangeLogHook
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("events")]
        public List<EntryChangeLogEvent> Events { get; set; } = new();
    }

    public sealed class EntryChangeLogEvent
    {
        [JsonPropertyName("eventId")]
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("timestampUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("performedBy")]
        public string PerformedBy { get; init; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; init; } = string.Empty;

        [JsonPropertyName("details")]
        public ChangeLogAttachmentDetails? Details { get; init; }
    }

    public sealed class ChangeLogAttachmentDetails
    {
        [JsonPropertyName("attachmentId")]
        public string AttachmentId { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("libraryPath")]
        public string LibraryPath { get; init; } = string.Empty;

        [JsonPropertyName("purpose")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AttachmentKind Purpose { get; init; } = AttachmentKind.Supplement;

        [JsonPropertyName("tags")]
        public List<string> Tags { get; init; } = new();
    }
}
