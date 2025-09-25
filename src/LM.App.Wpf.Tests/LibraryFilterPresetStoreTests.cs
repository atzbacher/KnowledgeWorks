using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LM.App.Wpf.Library;
using LM.Infrastructure.FileSystem;
using Xunit;

public class LibraryFilterPresetStoreTests
{
    [Fact]
    public async Task SaveLoadAndDelete_RoundTripsState()
    {
        using var temp = new TempDir();
        var workspace = new WorkspaceService();
        await workspace.EnsureWorkspaceAsync(temp.Path);

        var store = new LibraryFilterPresetStore(workspace);
        var preset = new LibraryFilterPreset
        {
            Name = "My Preset",
            State = new LibraryFilterState
            {
                UseFullTextSearch = true,
                UnifiedQuery = "title:heart",
                FullTextQuery = "heart",
                FullTextInTitle = false,
                FullTextInAbstract = true,
                FullTextInContent = false
            }
        };

        await store.SavePresetAsync(preset);

        var presets = await store.ListPresetsAsync();
        var loaded = Assert.Single(presets);
        Assert.Equal("My Preset", loaded.Name);
        Assert.True((DateTime.UtcNow - loaded.SavedUtc) < TimeSpan.FromMinutes(1));

        Assert.Equal(preset.State.UseFullTextSearch, loaded.State.UseFullTextSearch);
        Assert.Equal(preset.State.UnifiedQuery, loaded.State.UnifiedQuery);
        Assert.Equal(preset.State.FullTextQuery, loaded.State.FullTextQuery);
        Assert.Equal(preset.State.FullTextInTitle, loaded.State.FullTextInTitle);
        Assert.Equal(preset.State.FullTextInAbstract, loaded.State.FullTextInAbstract);
        Assert.Equal(preset.State.FullTextInContent, loaded.State.FullTextInContent);

        var fetched = await store.TryGetPresetAsync("my preset");
        Assert.NotNull(fetched);
        Assert.Equal("My Preset", fetched!.Name);

        await store.DeletePresetAsync("My Preset");
        var afterDelete = await store.ListPresetsAsync();
        Assert.Empty(afterDelete);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_preset_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
