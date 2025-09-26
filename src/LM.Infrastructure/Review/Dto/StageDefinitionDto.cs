using System.Collections.Generic;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Dto;

internal sealed class StageDefinitionDto : AuditableReviewDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stageType")]
    public ReviewStageType StageType { get; set; }
        = ReviewStageType.TitleScreening;

    [JsonPropertyName("reviewerRequirements")]
    public Dictionary<ReviewerRole, int> ReviewerRequirements { get; set; } = new();

    [JsonPropertyName("consensus")]
    public StageConsensusPolicyDto Consensus { get; set; } = new();
}

internal sealed class StageConsensusPolicyDto : AuditableReviewDto
{
    [JsonPropertyName("requiresConsensus")]
    public bool RequiresConsensus { get; set; }
        = false;

    [JsonPropertyName("minimumAgreements")]
    public int MinimumAgreements { get; set; }
        = 0;

    [JsonPropertyName("escalateOnDisagreement")]
    public bool EscalateOnDisagreement { get; set; }
        = false;

    [JsonPropertyName("arbitrationRole")]
    public ReviewerRole? ArbitrationRole { get; set; }
        = null;
}
