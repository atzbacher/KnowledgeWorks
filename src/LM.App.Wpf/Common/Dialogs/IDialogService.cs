#nullable enable
using LM.App.Wpf.ViewModels;

namespace LM.App.Wpf.Common.Dialogs
{
    public sealed record FilePickerOptions
    {
        public string? Filter { get; init; }
        public bool AllowMultiple { get; init; }
    }

    public sealed record FolderPickerOptions
    {
        public string? Description { get; init; }
    }

    public interface IDialogService
    {
        string[]? ShowOpenFileDialog(FilePickerOptions options);
        string? ShowFolderBrowserDialog(FolderPickerOptions options);
        bool? ShowStagingEditor(StagingListViewModel stagingList);
    }
}
