#nullable enable
using System;
using LM.App.Wpf.ViewModels.Review;

namespace LM.App.Wpf.Views.Review;

internal partial class StageWorkspacePreviewWindow : System.Windows.Window
{
    private StageWorkspacePreviewViewModel? _previewViewModel;

    public StageWorkspacePreviewWindow()
    {
        InitializeComponent();
    }

    public void Attach(StageBlueprintViewModel stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _previewViewModel?.Dispose();
        _previewViewModel = new StageWorkspacePreviewViewModel(stage);
        DataContext = _previewViewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _previewViewModel?.Dispose();
        _previewViewModel = null;
        base.OnClosed(e);
    }
}
