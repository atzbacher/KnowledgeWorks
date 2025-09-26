using System.Collections.Generic;
using LM.Infrastructure.Review.Dto;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Mappers;

internal static class ReviewStageMapper
{
    public static ReviewStageDto ToDto(ReviewStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var dto = new ReviewStageDto
        {
            Id = stage.Id,
            ProjectId = stage.ProjectId,
            DefinitionId = stage.Definition.Id,
            ConflictState = stage.ConflictState,
            ActivatedAt = stage.ActivatedAt,
            CompletedAt = stage.CompletedAt,
            Consensus = stage.Consensus is null ? null : ConsensusOutcomeMapper.ToDto(stage.Consensus)
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ReviewStage ToDomain(
        ReviewStageDto dto,
        StageDefinition definition,
        IReadOnlyCollection<ScreeningAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(assignments);

        var consensus = dto.Consensus is null ? null : ConsensusOutcomeMapper.ToDomain(dto.Consensus);

        return ReviewStage.Create(
            dto.Id,
            dto.ProjectId,
            definition,
            assignments,
            dto.ConflictState,
            dto.ActivatedAt,
            dto.CompletedAt,
            consensus);
    }
}

internal static class ConsensusOutcomeMapper
{
    public static ConsensusOutcomeDto ToDto(ConsensusOutcome consensus)
    {
        ArgumentNullException.ThrowIfNull(consensus);

        var dto = new ConsensusOutcomeDto
        {
            StageId = consensus.StageId,
            Approved = consensus.Approved,
            ResultingState = consensus.ResultingState,
            ResolvedAt = consensus.ResolvedAt,
            Notes = consensus.Notes,
            ResolvedBy = consensus.ResolvedBy
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ConsensusOutcome ToDomain(ConsensusOutcomeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return ConsensusOutcome.Create(dto.StageId, dto.Approved, dto.ResultingState, dto.ResolvedAt, dto.Notes, dto.ResolvedBy);
    }
}
