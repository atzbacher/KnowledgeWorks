using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public interface ILibraryEntryEditor
    {
        Task<bool> EditEntryAsync(Entry entry);
    }
}
