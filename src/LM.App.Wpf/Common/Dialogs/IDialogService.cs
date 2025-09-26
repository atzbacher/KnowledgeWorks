#nullable enable
using LM.App.Wpf.ViewModels;

namespace LM.App.Wpf.Common.Dialogs
{
    public sealed record FilePickerOptions
    {
        public string? Filter { get; init; }
        public bool AllowMultiple { get; init; }
    }

    public sealed record FileSavePickerOptions
    {
        public string? Filter { get; init; }
        public string? DefaultFileName { get; init; }
    }

    public sealed record FolderPickerOptions
    {
        public string? Description { get; init; }
    }

    public interface IDialogService
    {
        string[]? ShowOpenFileDialog(FilePickerOptions options);
        string? ShowFolderBrowserDialog(FolderPickerOptions options);
        string? ShowSaveFileDialog(FileSavePickerOptions options);
        bool? ShowStagingEditor(StagingListViewModel stagingList);
        bool? ShowDataExtractionWorkspace(StagingItem stagingItem);
    }
}
