using System;
using System.Text.Json.Serialization;

namespace LM.Infrastructure.Review.Dto;

internal sealed class ReviewAuditEntryDto : AuditableReviewDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; set; }
        = DateTimeOffset.UtcNow;

    [JsonPropertyName("details")]
    public string? Details { get; set; }
        = null;
}
