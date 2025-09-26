using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Forms;

namespace LM.Infrastructure.Review;

internal sealed partial class JsonReviewProjectStore
{
    private static class ProjectMapper
    {
        public static ProjectDocument FromDomain(ReviewProject project)
        {
            return new ProjectDocument
            {
                Id = project.Id,
                Name = project.Name,
                CreatedAt = project.CreatedAt,
                StageDefinitions = project.StageDefinitions.Select(StageDefinitionMapper.FromDomain).ToList(),
                AuditTrail = project.AuditTrail.Entries.Select(AuditEntryMapper.FromDomain).ToList()
            };
        }

        public static ReviewProject ToDomain(ProjectDocument doc)
        {
            var definitions = doc.StageDefinitions?.Select(StageDefinitionMapper.ToDomain).ToList() ?? new List<StageDefinition>();
            var auditEntries = doc.AuditTrail?.Select(AuditEntryMapper.ToDomain).ToList() ?? new List<ReviewAuditTrail.AuditEntry>();
            var auditTrail = ReviewAuditTrail.Create(auditEntries);
            return ReviewProject.Create(doc.Id, doc.Name, doc.CreatedAt, definitions, auditTrail);
        }
    }

    private static class StageMapper
    {
        public static StageDocument FromDomain(ReviewStage stage)
        {
            return new StageDocument
            {
                Id = stage.Id,
                ProjectId = stage.ProjectId,
                DefinitionId = stage.Definition.Id,
                ConflictState = stage.ConflictState,
                ActivatedAt = stage.ActivatedAt,
                CompletedAt = stage.CompletedAt,
                Consensus = stage.Consensus is null ? null : ConsensusOutcomeMapper.FromDomain(stage.Consensus)
            };
        }

        public static ReviewStage ToDomain(StageDocument doc, StageDefinition definition, IReadOnlyCollection<ScreeningAssignment> assignments)
        {
            var consensus = doc.Consensus is null ? null : ConsensusOutcomeMapper.ToDomain(doc.Consensus);
            return ReviewStage.Create(doc.Id, doc.ProjectId, definition, assignments, doc.ConflictState, doc.ActivatedAt, doc.CompletedAt, consensus);
        }
    }

    private static class AssignmentMapper
    {
        public static AssignmentDocument FromDomain(string projectId, ScreeningAssignment assignment)
        {
            return new AssignmentDocument
            {
                Id = assignment.Id,
                ProjectId = projectId,
                StageId = assignment.StageId,
                ReviewerId = assignment.ReviewerId,
                Role = assignment.Role,
                Status = assignment.Status,
                AssignedAt = assignment.AssignedAt,
                CompletedAt = assignment.CompletedAt,
                Decision = assignment.Decision is null ? null : ReviewerDecisionMapper.FromDomain(assignment.Decision)
            };
        }

        public static ScreeningAssignment ToDomain(AssignmentDocument doc)
        {
            var decision = doc.Decision is null ? null : ReviewerDecisionMapper.ToDomain(doc.Decision);
            return ScreeningAssignment.Create(doc.Id, doc.StageId, doc.ReviewerId, doc.Role, doc.Status, doc.AssignedAt, doc.CompletedAt, decision);
        }
    }

    private static class FormResponseMapper
    {
        public static FormResponseDocument FromDomain(FormResponse response, JsonSerializerOptions options)
        {
            return new FormResponseDocument
            {
                Id = response.Id,
                ProjectId = response.ProjectId,
                StageId = response.StageId,
                AssignmentId = response.AssignmentId,
                FormId = response.Snapshot.FormId,
                VersionId = response.Snapshot.VersionId,
                CapturedBy = response.Snapshot.CapturedBy,
                CapturedUtc = response.Snapshot.CapturedUtc,
                Values = response.Snapshot.Values.ToDictionary(
                    kvp => kvp.Key,
                    kvp => JsonSerializer.SerializeToElement(kvp.Value, options))
            };
        }

        public static FormResponse ToDomain(FormResponseDocument doc, JsonSerializerOptions options)
        {
            var elements = doc.Values ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var values = new Dictionary<string, object?>(elements.Count, StringComparer.Ordinal);
            foreach (var (key, element) in elements)
            {
                values[key] = element.Deserialize<object?>(options);
            }

            var snapshot = ExtractionFormSnapshot.Create(doc.FormId, doc.VersionId, values, doc.CapturedBy, doc.CapturedUtc);
            return new FormResponse(doc.Id, doc.ProjectId, doc.StageId, doc.AssignmentId, snapshot);
        }
    }

    private static class StageDefinitionMapper
    {
        public static StageDefinitionDocument FromDomain(StageDefinition definition)
        {
            return new StageDefinitionDocument
            {
                Id = definition.Id,
                Name = definition.Name,
                StageType = definition.StageType,
                ReviewerRequirements = definition.ReviewerRequirement.Requirements.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Consensus = StageConsensusPolicyMapper.FromDomain(definition.ConsensusPolicy)
            };
        }

        public static StageDefinition ToDomain(StageDefinitionDocument doc)
        {
            var requirement = ReviewerRequirement.Create(doc.ReviewerRequirements);
            var consensus = StageConsensusPolicyMapper.ToDomain(doc.Consensus ?? new StageConsensusPolicyDocument());
            return StageDefinition.Create(doc.Id, doc.Name, doc.StageType, requirement, consensus);
        }
    }

    private static class StageConsensusPolicyMapper
    {
        public static StageConsensusPolicyDocument FromDomain(StageConsensusPolicy policy)
        {
            return new StageConsensusPolicyDocument
            {
                RequiresConsensus = policy.RequiresConsensus,
                MinimumAgreements = policy.MinimumAgreements,
                EscalateOnDisagreement = policy.EscalateOnDisagreement,
                ArbitrationRole = policy.ArbitrationRole
            };
        }

        public static StageConsensusPolicy ToDomain(StageConsensusPolicyDocument doc)
        {
            if (!doc.RequiresConsensus)
            {
                return StageConsensusPolicy.Disabled();
            }

            return StageConsensusPolicy.RequireAgreement(doc.MinimumAgreements, doc.EscalateOnDisagreement, doc.ArbitrationRole);
        }
    }

    private static class AuditEntryMapper
    {
        public static AuditEntryDocument FromDomain(ReviewAuditTrail.AuditEntry entry)
        {
            return new AuditEntryDocument
            {
                Id = entry.Id,
                Actor = entry.Actor,
                Action = entry.Action,
                OccurredAt = entry.OccurredAt,
                Details = entry.Details
            };
        }

        public static ReviewAuditTrail.AuditEntry ToDomain(AuditEntryDocument doc)
        {
            return ReviewAuditTrail.AuditEntry.Create(doc.Id, doc.Actor, doc.Action, doc.OccurredAt, doc.Details);
        }
    }

    private static class ConsensusOutcomeMapper
    {
        public static ConsensusOutcomeDocument FromDomain(ConsensusOutcome consensus)
        {
            return new ConsensusOutcomeDocument
            {
                StageId = consensus.StageId,
                Approved = consensus.Approved,
                ResultingState = consensus.ResultingState,
                ResolvedAt = consensus.ResolvedAt,
                Notes = consensus.Notes,
                ResolvedBy = consensus.ResolvedBy
            };
        }

        public static ConsensusOutcome ToDomain(ConsensusOutcomeDocument doc)
        {
            return ConsensusOutcome.Create(doc.StageId, doc.Approved, doc.ResultingState, doc.ResolvedAt, doc.Notes, doc.ResolvedBy);
        }
    }

    private static class ReviewerDecisionMapper
    {
        public static ReviewerDecisionDocument FromDomain(ReviewerDecision decision)
        {
            return new ReviewerDecisionDocument
            {
                AssignmentId = decision.AssignmentId,
                ReviewerId = decision.ReviewerId,
                Decision = decision.Decision,
                DecidedAt = decision.DecidedAt,
                Notes = decision.Notes
            };
        }

        public static ReviewerDecision ToDomain(ReviewerDecisionDocument doc)
        {
            return ReviewerDecision.Create(doc.AssignmentId, doc.ReviewerId, doc.Decision, doc.DecidedAt, doc.Notes);
        }
    }
}
