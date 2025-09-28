using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Dto;

internal sealed class ReviewProjectDto : AuditableReviewDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    [JsonPropertyName("stageDefinitions")]
    public List<StageDefinitionDto> StageDefinitions { get; set; } = new();

    [JsonPropertyName("metadata")]
    public ReviewProjectMetadataDto Metadata { get; set; } = new();

    [JsonPropertyName("auditTrail")]
    public List<ReviewAuditEntryDto> AuditTrail { get; set; } = new();
}

internal sealed class ReviewProjectMetadataDto : AuditableReviewDto
{
    [JsonPropertyName("template")]
    public ReviewTemplateKind Template { get; set; } = ReviewTemplateKind.Custom;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
