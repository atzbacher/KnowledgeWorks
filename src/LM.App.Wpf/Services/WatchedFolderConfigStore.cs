#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LM.App.Wpf.Models;
using LM.Core.Abstractions;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Persists watched folder configuration under the workspace root.
    /// </summary>
    public sealed class WatchedFolderConfigStore : IWatchedFolderConfigStore
    {
        private readonly IWorkSpaceService _workspace;
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true
        };

        public WatchedFolderConfigStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public IReadOnlyList<WatchedFolder> Load()
        {
            try
            {
                var path = GetConfigPath();
                if (!File.Exists(path))
                    return Array.Empty<WatchedFolder>();

                using var stream = File.OpenRead(path);
                var folders = JsonSerializer.Deserialize<List<WatchedFolder>>(stream, s_jsonOptions);
                return folders ?? new List<WatchedFolder>();
            }
            catch
            {
                return Array.Empty<WatchedFolder>();
            }
        }

        public void Save(IEnumerable<WatchedFolder> folders)
        {
            var list = (folders ?? Enumerable.Empty<WatchedFolder>()).ToList();
            var path = GetConfigPath();
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);

            using var stream = File.Create(path);
            JsonSerializer.Serialize(stream, list, s_jsonOptions);
        }

        private string GetConfigPath()
        {
            var root = _workspace.GetWorkspaceRoot();
            var configDir = Path.Combine(root, "config");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, "watched-folders.json");
        }
    }
}

