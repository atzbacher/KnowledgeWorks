using System;
using System.Text.Json.Serialization;

namespace LM.Infrastructure.Review.Dto;

internal abstract class AuditableReviewDto
{
    [JsonPropertyName("modifiedBy")]
    public string ModifiedBy { get; set; } = string.Empty;

    [JsonPropertyName("modifiedUtc")]
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;
}
