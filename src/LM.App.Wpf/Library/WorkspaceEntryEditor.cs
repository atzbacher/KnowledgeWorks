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

            if (!EntryMetadataFile.TryEnsureExists(entry, metadataPath))

            {
                var message = $"Entry metadata could not be found or recreated at:\n{metadataPath}";
                if (TryRevealMetadata(metadataPath))
                {
                    System.Windows.MessageBox.Show(
                        message + "\nThe entry folder has been opened so you can inspect its contents.",
                        "Edit Entry",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        message,
                        "Edit Entry",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }

                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = metadataPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkspaceEntryEditor] Failed to open metadata directly: {ex}");

                if (TryRevealMetadata(metadataPath))

                    return;

                System.Windows.MessageBox.Show(
                    $"Failed to open entry metadata:\n{ex.Message}",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }


        private static bool TryRevealMetadata(string metadataPath)
        {
            var folder = Path.GetDirectoryName(metadataPath);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return false;

            try
            {
                if (File.Exists(metadataPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{metadataPath}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }

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
