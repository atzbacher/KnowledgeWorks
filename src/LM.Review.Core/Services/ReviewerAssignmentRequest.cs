using LM.Review.Core.Models;

namespace LM.Review.Core.Services;

public sealed record ReviewerAssignmentRequest(string ReviewerId, ReviewerRole Role);
