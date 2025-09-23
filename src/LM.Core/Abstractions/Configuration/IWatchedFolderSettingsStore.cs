using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions.Configuration
{
    /// <summary>Loads and persists watched folder configuration.</summary>
    public interface IWatchedFolderSettingsStore
    {
        Task<WatchedFolderSettings> LoadAsync(CancellationToken ct = default);
        Task SaveAsync(WatchedFolderSettings settings, CancellationToken ct = default);
    }
}
