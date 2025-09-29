using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    /// <summary>
    /// Handles document operations that require filesystem or process access for Library entries.
    /// </summary>
    public interface ILibraryDocumentService
    {
        Task OpenEntryAsync(Entry entry);
        Task OpenAttachmentAsync(Entry entry, Attachment attachment);
    }
}
