using System;
using System.Diagnostics;
using System.IO;

namespace LM.App.Wpf.Common
{
    /// <summary>
    /// Opens folders or selects files via Windows Explorer.
    /// </summary>
    public sealed class FileExplorerService : IFileExplorerService
    {
        public void RevealInExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must be provided.", nameof(path));

            var absolute = Path.GetFullPath(path);

            try
            {
                if (File.Exists(absolute))
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{absolute}\"",
                        UseShellExecute = true
                    };

                    Process.Start(info);
                    return;
                }

                if (!Directory.Exists(absolute))
                    throw new DirectoryNotFoundException($"Directory not found: {absolute}");

                var openFolder = new ProcessStartInfo
                {
                    FileName = absolute,
                    UseShellExecute = true
                };

                Process.Start(openFolder);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to open Explorer for '{absolute}'.", ex);
            }
        }
    }
}

