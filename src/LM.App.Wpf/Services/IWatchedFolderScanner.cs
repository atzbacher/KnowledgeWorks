#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Models;

namespace LM.App.Wpf.Services
{
    public interface IWatchedFolderScanner
    {
        Task<IReadOnlyList<string>> ScanAsync(WatchedFolder folder, CancellationToken ct);
    }
}

