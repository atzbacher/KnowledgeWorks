using System;
using System.Diagnostics;
using System.IO;
using LM.Core.Abstractions;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public sealed class WorkspaceEntryEditor : ILibraryEntryEditor
    {
        private readonly IWorkSpaceService _workspace;

        public WorkspaceEntryEditor(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public void EditEntry(Entry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                System.Windows.MessageBox.Show(
                    "Selected entry is missing an identifier.",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var relative = Path.Combine("entries", entry.Id, "entry.json");
            var metadataPath = _workspace.GetAbsolutePath(relative);
            var folder = Path.GetDirectoryName(metadataPath);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                System.Windows.MessageBox.Show(
                    $"Entry folder was not found at:\n{folder ?? metadataPath}",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            try
            {
                if (!File.Exists(metadataPath))
                {
                    System.Windows.MessageBox.Show(
                        $"Entry metadata was not found at:\n{metadataPath}",
                        "Edit Entry",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = metadataPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkspaceEntryEditor] Failed to open metadata directly: {ex}");
                if (TryOpenContainingFolder(metadataPath))
                    return;

                System.Windows.MessageBox.Show(
                    $"Failed to open entry metadata:\n{ex.Message}",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private static bool TryOpenContainingFolder(string metadataPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{metadataPath}\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkspaceEntryEditor] Explorer fallback failed: {ex}");
                return false;
            }
        }
    }
}
