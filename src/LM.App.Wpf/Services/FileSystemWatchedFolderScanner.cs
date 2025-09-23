#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Models;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Scans the file system for files under watched folders.
    /// </summary>
    public sealed class FileSystemWatchedFolderScanner : IWatchedFolderScanner
    {
        public Task<IReadOnlyList<string>> ScanAsync(WatchedFolder folder, CancellationToken ct)
        {
            if (folder is null) throw new ArgumentNullException(nameof(folder));
            if (string.IsNullOrWhiteSpace(folder.Path))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            if (!Directory.Exists(folder.Path))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            var option = folder.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = new List<string>();

            foreach (var file in Directory.EnumerateFiles(folder.Path, "*.*", option))
            {
                ct.ThrowIfCancellationRequested();
                files.Add(file);
            }

            return Task.FromResult<IReadOnlyList<string>>(files);
        }
    }
}

