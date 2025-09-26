using System.Collections.Generic;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Analytics;

namespace LM.Review.Core.Services;

public interface IReviewAnalyticsService
{
    ProjectAnalyticsSnapshot CreateSnapshot(
        ReviewProject project,
        IEnumerable<ReviewStage> stages,
        ReviewAnalyticsQueryOptions? options = null);
}
