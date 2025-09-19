#nullable enable
using System;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>Represents persisted state for a watched folder scan.</summary>
    public sealed record class WatchedFolderState(string Path,
                                                  DateTimeOffset? LastScanUtc,
                                                  string? AggregatedHash,
                                                  bool LastScanWasUnchanged);
}
