using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using Xunit;

public class WatchedFolderConfigTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesFolderState()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "lm_watched_" + Guid.NewGuid().ToString("N"));
        var configPath = tempDirectory + ".json";

        Directory.CreateDirectory(tempDirectory);
        var folderPath = Path.Combine(tempDirectory, "watched");
        Directory.CreateDirectory(folderPath);

        try
        {
            var original = new WatchedFolderConfig();
            var folder = new WatchedFolder { Path = folderPath, IsEnabled = true };
            original.Folders.Add(folder);

            var scanTime = DateTimeOffset.UtcNow;
            var state = new WatchedFolderState(folderPath, scanTime, "HASH", true);
            original.StoreState(folder, state);

            await original.SaveAsync(configPath, CancellationToken.None);

            var reloaded = await WatchedFolderConfig.LoadAsync(configPath, CancellationToken.None);
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
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }

                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests.
            }
        }
    }
}
