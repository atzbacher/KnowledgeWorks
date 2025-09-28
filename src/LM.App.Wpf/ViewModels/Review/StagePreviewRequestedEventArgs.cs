#nullable enable
using System;

namespace LM.App.Wpf.ViewModels.Review;

public sealed class StagePreviewRequestedEventArgs : EventArgs
{
    public StagePreviewRequestedEventArgs(StageBlueprintViewModel stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        Stage = stage;
    }

    public StageBlueprintViewModel Stage { get; }
}
