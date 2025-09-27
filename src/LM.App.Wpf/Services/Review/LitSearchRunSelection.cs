#nullable enable

namespace LM.App.Wpf.Services.Review
{
    internal sealed record LitSearchRunSelection(
        string EntryId,
        string HookAbsolutePath,
        string HookRelativePath,
        string RunId,
        string? CheckedEntriesAbsolutePath,
        string? CheckedEntriesRelativePath);
}
