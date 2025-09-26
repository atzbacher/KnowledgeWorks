using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models;

namespace LM.App.Wpf.Services.Review
{
    internal interface IReviewProjectLauncher
    {
        Task<ReviewProject?> CreateProjectAsync(CancellationToken cancellationToken);

        Task<ReviewProject?> LoadProjectAsync(CancellationToken cancellationToken);
    }
}
