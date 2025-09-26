using LM.Review.Core.Models;

namespace LM.Review.Core.Services;

public interface IReviewHookContextFactory
{
    IReviewHookContext CreateAssignmentUpdated(ReviewStage stage, ScreeningAssignment assignment);

    IReviewHookContext CreateReviewerDecisionRecorded(ScreeningAssignment assignment, ReviewerDecision decision);

    IReviewHookContext CreateStageTransition(ReviewStage stage, ConflictState previousState, ConflictState currentState);
}
