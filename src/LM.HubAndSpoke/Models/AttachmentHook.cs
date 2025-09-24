#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LM.Core.Models;

namespace LM.HubSpoke.Models
{
    public sealed class AttachmentHook
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("attachments")]
        public List<AttachmentHookItem> Attachments { get; set; } = new();
    }

    public sealed class AttachmentHookItem
    {
        [JsonPropertyName("attachmentId")]
        public string AttachmentId { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("libraryPath")]
        public string LibraryPath { get; init; } = string.Empty;

        [JsonPropertyName("tags")]
        public List<string> Tags { get; init; } = new();

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("purpose")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AttachmentKind Purpose { get; init; } = AttachmentKind.Supplement;

        [JsonPropertyName("addedBy")]
        public string AddedBy { get; init; } = string.Empty;

        [JsonPropertyName("addedUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime AddedUtc { get; init; } = DateTime.UtcNow;
    }
}
