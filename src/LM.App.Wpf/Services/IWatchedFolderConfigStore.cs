#nullable enable
using System.Collections.Generic;
using LM.App.Wpf.Models;

namespace LM.App.Wpf.Services
{
    public interface IWatchedFolderConfigStore
    {
        IReadOnlyList<WatchedFolder> Load();
        void Save(IEnumerable<WatchedFolder> folders);
    }
}

