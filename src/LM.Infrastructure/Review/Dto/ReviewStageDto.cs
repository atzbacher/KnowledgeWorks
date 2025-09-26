using System;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Dto;

internal sealed class ReviewStageDto : AuditableReviewDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("definitionId")]
    public string DefinitionId { get; set; } = string.Empty;

    [JsonPropertyName("conflictState")]
    public ConflictState ConflictState { get; set; } = ConflictState.None;

    [JsonPropertyName("activatedAt")]
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }
        = null;

    [JsonPropertyName("consensus")]
    public ConsensusOutcomeDto? Consensus { get; set; }
        = null;
}

internal sealed class ConsensusOutcomeDto : AuditableReviewDto
{
    [JsonPropertyName("stageId")]
    public string StageId { get; set; } = string.Empty;

    [JsonPropertyName("approved")]
    public bool Approved { get; set; }
        = false;

    [JsonPropertyName("resultingState")]
    public ConflictState ResultingState { get; set; }
        = ConflictState.None;

    [JsonPropertyName("resolvedAt")]
    public DateTimeOffset ResolvedAt { get; set; }
        = DateTimeOffset.UtcNow;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
        = null;

    [JsonPropertyName("resolvedBy")]
    public string? ResolvedBy { get; set; }
        = null;
}
