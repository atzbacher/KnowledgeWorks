using System;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Dto;

internal sealed class ScreeningAssignmentDto : AuditableReviewDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("stageId")]
    public string StageId { get; set; } = string.Empty;

    [JsonPropertyName("reviewerId")]
    public string ReviewerId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public ReviewerRole Role { get; set; } = ReviewerRole.Primary;

    [JsonPropertyName("status")]
    public ScreeningStatus Status { get; set; } = ScreeningStatus.Pending;

    [JsonPropertyName("assignedAt")]
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }
        = null;

    [JsonPropertyName("decision")]
    public ReviewerDecisionDto? Decision { get; set; }
        = null;
}

internal sealed class ReviewerDecisionDto : AuditableReviewDto
{
    [JsonPropertyName("assignmentId")]
    public string AssignmentId { get; set; } = string.Empty;

    [JsonPropertyName("reviewerId")]
    public string ReviewerId { get; set; } = string.Empty;

    [JsonPropertyName("decision")]
    public ScreeningStatus Decision { get; set; } = ScreeningStatus.Pending;

    [JsonPropertyName("decidedAt")]
    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
        = null;
}
