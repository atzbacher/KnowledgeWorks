using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.Common;

namespace LM.App.Wpf.ViewModels.Library
{
    public enum LibraryNavigationNodeKind
    {
        Category,
        SavedSearch,
        LitSearchEntry,
        LitSearchRun
    }

    public sealed partial class LibraryNavigationNodeViewModel : ObservableObject
    {
        public LibraryNavigationNodeViewModel(string name, LibraryNavigationNodeKind kind)
        {
            Name = name;
            Kind = kind;
        }

        public string Name { get; }
        public LibraryNavigationNodeKind Kind { get; }

        [ObservableProperty]
        private string? subtitle;

        public ObservableCollection<LibraryNavigationNodeViewModel> Children { get; } = new();

        public object? Payload { get; init; }

        public bool HasChildren => Children.Count > 0;
    }

    internal sealed record LibrarySavedSearchPayload(LibraryPresetSummary Summary);

    internal sealed record LibraryLitSearchEntryPayload(string EntryId, string HookPath, string Title, string? Query);

    internal sealed record LibraryLitSearchRunPayload(string EntryId, string RunId, string? CheckedEntriesPath, string? Label);
}
