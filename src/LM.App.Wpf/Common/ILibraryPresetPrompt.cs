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

    public sealed record LibraryPresetSaveContext(string DefaultName, IReadOnlyCollection<string> ExistingNames);

    public sealed record LibraryPresetSaveResult(string Name);

    public sealed record LibraryPresetSelectionContext(
        IReadOnlyList<LibraryPresetSummary> Presets,
        bool AllowLoad,
        string Title);

    public sealed record LibraryPresetSelectionResult(string? SelectedPresetName, IReadOnlyList<string> DeletedPresetNames);

    public sealed record LibraryPresetSummary(string Name, DateTime SavedUtc);
}
