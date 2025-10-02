using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Library
{
    public sealed class LibrarySortOption
    {
        public LibrarySortOption(string key, string displayName)
        {
            Key = key ?? throw new System.ArgumentNullException(nameof(key));
            DisplayName = displayName ?? throw new System.ArgumentNullException(nameof(displayName));
        }

        public string Key { get; }

        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class LibrarySortOptions
    {
        public static LibrarySortOption NewestFirst { get; } = new("Newest", "Newest first");

        public static LibrarySortOption OldestFirst { get; } = new("Oldest", "Oldest first");

        public static LibrarySortOption TitleAscending { get; } = new("TitleAsc", "Title (A-Z)");

        public static LibrarySortOption TitleDescending { get; } = new("TitleDesc", "Title (Z-A)");

        public static IReadOnlyList<LibrarySortOption> All { get; } = new[]
        {
            NewestFirst,
            OldestFirst,
            TitleAscending,
            TitleDescending
        };
    }
}
