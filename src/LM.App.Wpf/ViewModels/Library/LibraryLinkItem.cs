using System;

namespace LM.App.Wpf.ViewModels.Library
{
    public enum LinkItemKind
    {
        Url,
        File,
        Folder
    }

    public sealed record LibraryLinkItem(string DisplayText, string Target, LinkItemKind Kind)
    {
        public string DisplayText { get; init; } = DisplayText ?? throw new ArgumentNullException(nameof(DisplayText));

        public string Target { get; init; } = Target ?? throw new ArgumentNullException(nameof(Target));

        public LinkItemKind Kind { get; init; } = Kind;
    }
}
