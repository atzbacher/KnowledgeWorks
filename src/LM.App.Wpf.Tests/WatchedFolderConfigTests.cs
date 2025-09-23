using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.Core.Models;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Settings;
using Xunit;

public class WatchedFolderConfigTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesFolderState()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "lm_watched_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(workspaceRoot);
            var store = new JsonWatchedFolderSettingsStore(workspace);

            var original = new WatchedFolderConfig();
            var folderPath = Path.Combine(workspaceRoot, "watched");
            Directory.CreateDirectory(folderPath);

            var folder = new WatchedFolder { Path = folderPath, IsEnabled = true };
            original.Folders.Add(folder);

            var scanTime = DateTimeOffset.UtcNow;
            var state = new WatchedFolderState(folderPath, scanTime, "HASH", true);
            original.StoreState(folder, state);

            await store.SaveAsync(original.CreateSnapshot(), CancellationToken.None);

            var loadedSettings = await store.LoadAsync(CancellationToken.None);
            var reloaded = new WatchedFolderConfig();
            reloaded.Load(loadedSettings);

            Assert.Single(reloaded.Folders);

            var reloadedFolder = reloaded.Folders[0];
            Assert.Equal(folderPath, reloadedFolder.Path);
            Assert.Equal(scanTime, reloadedFolder.LastScanUtc);
            Assert.True(reloadedFolder.LastScanWasUnchanged);

            var reloadedState = reloaded.GetState(folderPath);
            Assert.NotNull(reloadedState);
            Assert.Equal("HASH", reloadedState!.AggregatedHash);
            Assert.Equal(scanTime, reloadedState.LastScanUtc);
            Assert.True(reloadedState.LastScanWasUnchanged);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workspaceRoot))
                {
                    Directory.Delete(workspaceRoot, recursive: true);
                }

                var configPath = BuildConfigPath(workspaceRoot);
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests.
            }
        }
    }

    private static string BuildConfigPath(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var trimmed = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0)
        {
            trimmed = fullRoot;
        }

        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name))
        {
            name = "workspace";
        }

        var directory = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrEmpty(directory))
        {
            directory = fullRoot;
        }

        return Path.Combine(directory, $"{name}.watched-folders.json");
    }
}
