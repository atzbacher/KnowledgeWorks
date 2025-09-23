#nullable enable
using System.Collections.Generic;
using LM.App.Wpf.ViewModels.Add;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Abstraction over UI dialogs so that view models remain testable.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an open-file dialog and returns the selected files.
        /// </summary>
        /// <param name="filter">Filter string passed to the dialog.</param>
        /// <param name="allowMultiple">Whether multiple selection is allowed.</param>
        /// <returns>List of selected file paths; empty when cancelled.</returns>
        IReadOnlyList<string> ShowOpenFileDialog(string filter, bool allowMultiple);

        /// <summary>
        /// Shows a folder browser dialog and returns the selected path.
        /// </summary>
        /// <param name="description">Dialog description text.</param>
        /// <returns>Absolute path of the selected folder, or null when cancelled.</returns>
        string? ShowFolderBrowserDialog(string description);

        /// <summary>
        /// Opens the staging editor using the provided view model.
        /// </summary>
        void ShowStagingEditor(StagingListViewModel stagingViewModel);
    }
}

