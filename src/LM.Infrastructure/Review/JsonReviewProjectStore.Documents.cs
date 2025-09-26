using System;
using System.Collections.Generic;
using System.Text.Json;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Forms;

namespace LM.Infrastructure.Review;

internal sealed partial class JsonReviewProjectStore
{
    internal sealed record FormResponse(string Id, string ProjectId, string? StageId, string? AssignmentId, ExtractionFormSnapshot Snapshot);

    private sealed class FormResponseDocument
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string? StageId { get; set; }
        public string? AssignmentId { get; set; }
        public string FormId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string CapturedBy { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);
    }
}
