using System;
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
                TitleContains = "title",
                AuthorContains = "author",
                TagsCsv = "tag1,tag2",
                IsInternal = true,
                YearFrom = 2010,
                YearTo = 2020,
                SourceContains = "source",
                InternalIdContains = "internal",
                DoiContains = "doi",
                PmidContains = "pmid",
                NctContains = "nct",
                AddedByContains = "adder",
                AddedOnFrom = new DateTime(2024, 1, 1),
                AddedOnTo = new DateTime(2024, 12, 31),
                TypePublication = false,
                TypePresentation = true,
                TypeWhitePaper = false,
                TypeSlideDeck = true,
                TypeReport = false,
                TypeOther = true
            }
        };

        await store.SavePresetAsync(preset);

        var presets = await store.ListPresetsAsync();
        var loaded = Assert.Single(presets);
        Assert.Equal("My Preset", loaded.Name);
        Assert.True((DateTime.UtcNow - loaded.SavedUtc) < TimeSpan.FromMinutes(1));

        Assert.Equal(preset.State.TitleContains, loaded.State.TitleContains);
        Assert.Equal(preset.State.AuthorContains, loaded.State.AuthorContains);
        Assert.Equal(preset.State.TagsCsv, loaded.State.TagsCsv);
        Assert.Equal(preset.State.IsInternal, loaded.State.IsInternal);
        Assert.Equal(preset.State.YearFrom, loaded.State.YearFrom);
        Assert.Equal(preset.State.YearTo, loaded.State.YearTo);
        Assert.Equal(preset.State.SourceContains, loaded.State.SourceContains);
        Assert.Equal(preset.State.InternalIdContains, loaded.State.InternalIdContains);
        Assert.Equal(preset.State.DoiContains, loaded.State.DoiContains);
        Assert.Equal(preset.State.PmidContains, loaded.State.PmidContains);
        Assert.Equal(preset.State.NctContains, loaded.State.NctContains);
        Assert.Equal(preset.State.AddedByContains, loaded.State.AddedByContains);
        Assert.Equal(preset.State.AddedOnFrom, loaded.State.AddedOnFrom);
        Assert.Equal(preset.State.AddedOnTo, loaded.State.AddedOnTo);
        Assert.Equal(preset.State.TypePublication, loaded.State.TypePublication);
        Assert.Equal(preset.State.TypePresentation, loaded.State.TypePresentation);
        Assert.Equal(preset.State.TypeWhitePaper, loaded.State.TypeWhitePaper);
        Assert.Equal(preset.State.TypeSlideDeck, loaded.State.TypeSlideDeck);
        Assert.Equal(preset.State.TypeReport, loaded.State.TypeReport);
        Assert.Equal(preset.State.TypeOther, loaded.State.TypeOther);

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
