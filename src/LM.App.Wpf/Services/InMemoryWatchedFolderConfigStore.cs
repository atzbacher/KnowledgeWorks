#nullable enable
using System.Collections.Generic;
using System.Linq;
using LM.App.Wpf.Models;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Lightweight in-memory store used when persistence is unavailable.
    /// </summary>
    public sealed class InMemoryWatchedFolderConfigStore : IWatchedFolderConfigStore
    {
        private readonly List<WatchedFolder> _folders = new();

        public IReadOnlyList<WatchedFolder> Load() => _folders.ToList();

        public void Save(IEnumerable<WatchedFolder> folders)
        {
            _folders.Clear();
            if (folders is null) return;
            _folders.AddRange(folders);
        }
    }
}

