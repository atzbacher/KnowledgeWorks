using System;
using LM.Core.Utils;
using LM.Infrastructure.Review.Dto;

namespace LM.Infrastructure.Review.Mappers;

internal static class ReviewDtoAuditStamp
{
    public static T Stamp<T>(T dto)
        where T : AuditableReviewDto
    {
        dto.ModifiedBy = SystemUser.GetCurrent();
        dto.ModifiedUtc = DateTimeOffset.UtcNow;
        return dto;
    }
}
