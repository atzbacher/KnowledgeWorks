using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models;

namespace LM.Review.Core.Services;

public interface IReviewWorkflowStore
{
    Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken);

    Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken cancellationToken);

    Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken);

    Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken);

    Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken);
}
