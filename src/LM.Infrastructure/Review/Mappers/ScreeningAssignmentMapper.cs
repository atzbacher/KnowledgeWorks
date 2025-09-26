using LM.Infrastructure.Review.Dto;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Mappers;

internal static class ScreeningAssignmentMapper
{
    public static ScreeningAssignmentDto ToDto(string projectId, ScreeningAssignment assignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(assignment);

        var dto = new ScreeningAssignmentDto
        {
            Id = assignment.Id,
            ProjectId = projectId,
            StageId = assignment.StageId,
            ReviewerId = assignment.ReviewerId,
            Role = assignment.Role,
            Status = assignment.Status,
            AssignedAt = assignment.AssignedAt,
            CompletedAt = assignment.CompletedAt,
            Decision = assignment.Decision is null ? null : ReviewerDecisionMapper.ToDto(assignment.Decision)
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ScreeningAssignment ToDomain(ScreeningAssignmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var decision = dto.Decision is null ? null : ReviewerDecisionMapper.ToDomain(dto.Decision);

        return ScreeningAssignment.Create(
            dto.Id,
            dto.StageId,
            dto.ReviewerId,
            dto.Role,
            dto.Status,
            dto.AssignedAt,
            dto.CompletedAt,
            decision);
    }
}

internal static class ReviewerDecisionMapper
{
    public static ReviewerDecisionDto ToDto(ReviewerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var dto = new ReviewerDecisionDto
        {
            AssignmentId = decision.AssignmentId,
            ReviewerId = decision.ReviewerId,
            Decision = decision.Decision,
            DecidedAt = decision.DecidedAt,
            Notes = decision.Notes
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ReviewerDecision ToDomain(ReviewerDecisionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return ReviewerDecision.Create(dto.AssignmentId, dto.ReviewerId, dto.Decision, dto.DecidedAt, dto.Notes);
    }
}
