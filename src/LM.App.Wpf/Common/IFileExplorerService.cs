using System;

namespace LM.App.Wpf.Common
{
    /// <summary>
    /// Launches Explorer windows for workspace content.
    /// </summary>
    public interface IFileExplorerService
    {
        void RevealInExplorer(string path);
    }
}

