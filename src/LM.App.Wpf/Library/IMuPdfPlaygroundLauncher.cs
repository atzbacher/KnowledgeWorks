#nullable enable
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public interface IMuPdfPlaygroundLauncher
    {
        Task<bool> LaunchAsync(Entry entry, CancellationToken cancellationToken);
    }
}
