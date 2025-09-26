using System;
using LM.Infrastructure.Review.Dto;

namespace LM.Infrastructure.Review.Mappers;

internal static class ReviewDtoAuditStamp
{
    public static T Stamp<T>(T dto)
        where T : AuditableReviewDto
    {
        dto.ModifiedBy = Environment.UserName ?? string.Empty;
        dto.ModifiedUtc = DateTimeOffset.UtcNow;
        return dto;
    }
}
