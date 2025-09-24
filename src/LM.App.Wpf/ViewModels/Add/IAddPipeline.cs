#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels
{
    public interface IAddPipeline
    {
        Task<IReadOnlyList<StagingItem>> StagePathsAsync(IEnumerable<string> paths, CancellationToken ct);
        Task<IReadOnlyList<StagingItem>> CommitAsync(IEnumerable<StagingItem> selectedRows, CancellationToken ct);
    }
}
