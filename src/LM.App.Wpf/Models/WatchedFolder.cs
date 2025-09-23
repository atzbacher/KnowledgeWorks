#nullable enable

namespace LM.App.Wpf.Models
{
    public sealed class WatchedFolder
    {
        public string Path { get; init; } = string.Empty;
        public bool IncludeSubdirectories { get; init; } = true;
        public bool IsEnabled { get; init; } = true;
    }
}

