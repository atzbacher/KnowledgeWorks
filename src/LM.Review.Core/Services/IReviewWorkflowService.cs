using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models;

namespace LM.Review.Core.Services;

public interface IReviewWorkflowService
{
    Task<ReviewStage> CreateStageAsync(
        string projectId,
        string stageDefinitionId,
        IReadOnlyCollection<ReviewerAssignmentRequest> assignments,
        CancellationToken cancellationToken = default);

    Task<ScreeningAssignment> SubmitDecisionAsync(
        string assignmentId,
        ScreeningStatus decision,
        string? notes,
        CancellationToken cancellationToken = default);
}
