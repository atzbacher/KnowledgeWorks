using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LM.App.Wpf.Common
{
    public interface ILibraryPresetPrompt
    {
        Task<LibraryPresetSaveResult?> RequestSaveAsync(LibraryPresetSaveContext context);
        Task<LibraryPresetSelectionResult?> RequestSelectionAsync(LibraryPresetSelectionContext context);
    }

    public sealed record LibraryPresetSaveContext(
        string DefaultName,
        IReadOnlyCollection<string> ExistingNames,
        string Title = "Save Library Preset",
        string Prompt = "Name this filter preset.");

    public sealed record LibraryPresetSaveResult(string Name);

    public sealed record LibraryPresetSelectionContext(
        IReadOnlyList<LibraryPresetSummary> Presets,
        bool AllowLoad,
        string Title);

    public sealed record LibraryPresetSelectionResult(string? SelectedPresetId, IReadOnlyList<string> DeletedPresetIds);

    public sealed record LibraryPresetSummary(string Id, string Name, DateTime SavedUtc);
}
