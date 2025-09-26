using System.Threading;
using System.Threading.Tasks;

namespace LM.Review.Core.Services;

public interface IReviewHookOrchestrator
{
    Task ProcessAsync(string entryId, IReviewHookContext context, CancellationToken cancellationToken);
}
