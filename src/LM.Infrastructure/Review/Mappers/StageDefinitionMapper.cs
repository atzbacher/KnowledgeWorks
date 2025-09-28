using System.Collections.Generic;
using System.Linq;
using LM.Infrastructure.Review.Dto;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Mappers;

internal static class StageDefinitionMapper
{
    public static StageDefinitionDto ToDto(StageDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var dto = new StageDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            StageType = definition.StageType,
            ReviewerRequirements = definition.ReviewerRequirement.Requirements
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Consensus = StageConsensusPolicyMapper.ToDto(definition.ConsensusPolicy),
            DisplayAreas = definition.DisplayProfile.ContentAreas.ToList()
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static StageDefinition ToDomain(StageDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var requirements = dto.ReviewerRequirements ?? new Dictionary<ReviewerRole, int>();
        var requirement = ReviewerRequirement.Create(requirements);
        var consensus = StageConsensusPolicyMapper.ToDomain(dto.Consensus ?? new StageConsensusPolicyDto());
        var displayAreas = dto.DisplayAreas ?? new List<StageContentArea>();

        if (displayAreas.Count == 0)
        {
            displayAreas = new List<StageContentArea>
            {
                StageContentArea.BibliographySummary,
                StageContentArea.ReviewerDecisionPanel
            };
        }

        var displayProfile = StageDisplayProfile.Create(displayAreas);

        return StageDefinition.Create(dto.Id, dto.Name, dto.StageType, requirement, consensus, displayProfile);
    }
}

internal static class StageConsensusPolicyMapper
{
    public static StageConsensusPolicyDto ToDto(StageConsensusPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var dto = new StageConsensusPolicyDto
        {
            RequiresConsensus = policy.RequiresConsensus,
            MinimumAgreements = policy.MinimumAgreements,
            EscalateOnDisagreement = policy.EscalateOnDisagreement,
            ArbitrationRole = policy.ArbitrationRole
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static StageConsensusPolicy ToDomain(StageConsensusPolicyDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!dto.RequiresConsensus)
        {
            return StageConsensusPolicy.Disabled();
        }

        return StageConsensusPolicy.RequireAgreement(dto.MinimumAgreements, dto.EscalateOnDisagreement, dto.ArbitrationRole);
    }
}
