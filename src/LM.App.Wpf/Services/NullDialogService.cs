#nullable enable
using System.Collections.Generic;
using LM.App.Wpf.ViewModels.Add;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// No-op dialog service used for tests or legacy constructors.
    /// </summary>
    public sealed class NullDialogService : IDialogService
    {
        public IReadOnlyList<string> ShowOpenFileDialog(string filter, bool allowMultiple)
            => System.Array.Empty<string>();

        public string? ShowFolderBrowserDialog(string description) => null;

        public void ShowStagingEditor(StagingListViewModel stagingViewModel)
        {
            // intentionally left blank
        }
    }
}

