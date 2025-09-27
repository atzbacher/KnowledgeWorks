namespace LM.App.Wpf.Services;

using LM.Review.Core.Models;

internal interface IReviewAuditService
{
    void Append(ReviewProjectDefinition project);
}
