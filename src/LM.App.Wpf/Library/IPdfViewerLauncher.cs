using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public interface IPdfViewerLauncher
    {
        Task<bool> LaunchAsync(Entry entry, string? attachmentId = null);
    }
}
