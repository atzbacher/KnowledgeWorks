using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        var hierarchy = await store.GetHierarchyAsync();
        var savedPreset = Assert.Single(hierarchy.Presets);
        Assert.False(string.IsNullOrWhiteSpace(savedPreset.Id));

        var fetchedById = await store.TryGetPresetByIdAsync(savedPreset.Id!);
        Assert.NotNull(fetchedById);
        Assert.Equal("My Preset", fetchedById!.Name);

        var presets = await store.ListPresetsAsync();
        var loaded = Assert.Single(presets);
        Assert.Equal(savedPreset.Id, loaded.Id);
        Assert.Equal(preset.State.UnifiedQuery, loaded.State.UnifiedQuery);

        await store.DeletePresetAsync(savedPreset.Id!);
        var afterDelete = await store.ListPresetsAsync();
        Assert.Empty(afterDelete);
    }

    [Fact]
    public async Task LegacyFlatData_MigratesIntoRootFolder()
    {
        using var temp = new TempDir();
        var workspace = new WorkspaceService();
        await workspace.EnsureWorkspaceAsync(temp.Path);

        var legacy = new
        {
            presets = new[]
            {
                new
                {
                    Name = "Legacy",
                    SavedUtc = DateTime.UtcNow,
                    State = new { UnifiedQuery = "tag:legacy" }
                }
            }
        };

        var path = Path.Combine(temp.Path, "library", "filter-presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy));

        var store = new LibraryFilterPresetStore(workspace);
        var hierarchy = await store.GetHierarchyAsync();
        var migrated = Assert.Single(hierarchy.Presets);
        Assert.Equal("Legacy", migrated.Name);
        Assert.False(string.IsNullOrWhiteSpace(migrated.Id));

        var list = await store.ListPresetsAsync();
        Assert.Single(list);
        Assert.Equal("Legacy", list.Single().Name);
    }

    [Fact]
    public async Task MovePresetBetweenFolders_UpdatesHierarchy()
    {
        using var temp = new TempDir();
        var workspace = new WorkspaceService();
        await workspace.EnsureWorkspaceAsync(temp.Path);

        var store = new LibraryFilterPresetStore(workspace);
        var presetA = new LibraryFilterPreset { Name = "Preset A" };
        var presetB = new LibraryFilterPreset { Name = "Preset B" };

        await store.SavePresetAsync(presetA);
        await store.SavePresetAsync(presetB);

        var folderId = await store.CreateFolderAsync(LibraryPresetFolder.RootId, "Folder");

        var rootBeforeMove = await store.GetHierarchyAsync();
        var ids = rootBeforeMove.Presets.Select(p => p.Id!).ToArray();

        await store.MovePresetAsync(ids[0], folderId, 0);

        var rootAfterMove = await store.GetHierarchyAsync();
        Assert.Single(rootAfterMove.Presets);
        var folder = Assert.Single(rootAfterMove.Folders);
        Assert.Equal(folderId, folder.Id);
        var moved = Assert.Single(folder.Presets);
        Assert.Equal(ids[0], moved.Id);

        await store.MovePresetAsync(ids[0], LibraryPresetFolder.RootId, 1);
        var rootAfterReturn = await store.GetHierarchyAsync();
        Assert.Equal(2, rootAfterReturn.Presets.Count);
        Assert.Empty(rootAfterReturn.Folders.Single().Presets);
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
