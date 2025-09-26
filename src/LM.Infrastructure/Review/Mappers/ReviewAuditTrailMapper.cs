using LM.Infrastructure.Review.Dto;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Mappers;

internal static class ReviewAuditTrailMapper
{
    public static ReviewAuditEntryDto ToDto(ReviewAuditTrail.AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var dto = new ReviewAuditEntryDto
        {
            Id = entry.Id,
            Actor = entry.Actor,
            Action = entry.Action,
            OccurredAt = entry.OccurredAt,
            Details = entry.Details
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ReviewAuditTrail.AuditEntry ToDomain(ReviewAuditEntryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return ReviewAuditTrail.AuditEntry.Create(dto.Id, dto.Actor, dto.Action, dto.OccurredAt, dto.Details);
    }
}
