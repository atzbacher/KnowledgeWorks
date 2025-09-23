using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>Serializable payload for watched folder configuration.</summary>
    public sealed record class WatchedFolderSettings
    {
        public IReadOnlyList<WatchedFolderSettingsFolder> Folders { get; init; }
            = System.Array.Empty<WatchedFolderSettingsFolder>();

        public IReadOnlyList<WatchedFolderState> States { get; init; }
            = System.Array.Empty<WatchedFolderState>();
    }

    /// <summary>Serializable entry describing a watched folder.</summary>
    public sealed record class WatchedFolderSettingsFolder
    {
        public string Path { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
            = true;
    }
}
