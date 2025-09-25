using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    /// <summary>
    /// Handles document operations that require filesystem or process access for Library entries.
    /// </summary>
    public interface ILibraryDocumentService
    {
        void OpenEntry(Entry entry);
        void OpenAttachment(Attachment attachment);
    }
}
