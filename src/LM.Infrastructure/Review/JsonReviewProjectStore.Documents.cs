using System;
using System.Collections.Generic;
using System.Text.Json;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Forms;

namespace LM.Infrastructure.Review;

internal sealed partial class JsonReviewProjectStore
{
    internal sealed record FormResponse(string Id, string ProjectId, string? StageId, string? AssignmentId, ExtractionFormSnapshot Snapshot);

    private sealed class ProjectDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public List<StageDefinitionDocument> StageDefinitions { get; set; } = new();
        public List<AuditEntryDocument> AuditTrail { get; set; } = new();
    }

    private sealed class StageDocument
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public ConflictState ConflictState { get; set; }
        public DateTimeOffset ActivatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public ConsensusOutcomeDocument? Consensus { get; set; }
    }

    private sealed class AssignmentDocument
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string StageId { get; set; } = string.Empty;
        public string ReviewerId { get; set; } = string.Empty;
        public ReviewerRole Role { get; set; }
        public ScreeningStatus Status { get; set; }
        public DateTimeOffset AssignedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public ReviewerDecisionDocument? Decision { get; set; }
    }

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

    private sealed class StageDefinitionDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ReviewStageType StageType { get; set; }
        public Dictionary<ReviewerRole, int> ReviewerRequirements { get; set; } = new();
        public StageConsensusPolicyDocument Consensus { get; set; } = new();
    }

    private sealed class StageConsensusPolicyDocument
    {
        public bool RequiresConsensus { get; set; }
        public int MinimumAgreements { get; set; }
        public bool EscalateOnDisagreement { get; set; }
        public ReviewerRole? ArbitrationRole { get; set; }
    }

    private sealed class AuditEntryDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTimeOffset OccurredAt { get; set; }
        public string? Details { get; set; }
    }

    private sealed class ConsensusOutcomeDocument
    {
        public string StageId { get; set; } = string.Empty;
        public bool Approved { get; set; }
        public ConflictState ResultingState { get; set; }
        public DateTimeOffset ResolvedAt { get; set; }
        public string? Notes { get; set; }
        public string? ResolvedBy { get; set; }
    }

    private sealed class ReviewerDecisionDocument
    {
        public string AssignmentId { get; set; } = string.Empty;
        public string ReviewerId { get; set; } = string.Empty;
        public ScreeningStatus Decision { get; set; }
        public DateTimeOffset DecidedAt { get; set; }
        public string? Notes { get; set; }
    }
}
