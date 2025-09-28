#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class RoiSelectionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isSelecting;

    [ObservableProperty]
    private bool hasSelection;

    [ObservableProperty]
    private double x;

    [ObservableProperty]
    private double y;

    [ObservableProperty]
    private double width;

    [ObservableProperty]
    private double height;

    public bool HasVisibleSelection => IsSelecting || HasSelection;

    partial void OnIsSelectingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleSelection));
    }

    partial void OnHasSelectionChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleSelection));
    }

    public void Begin(System.Windows.Point start)
    {
        IsSelecting = true;
        HasSelection = false;
        X = start.X;
        Y = start.Y;
        Width = 0;
        Height = 0;
    }

    public void Update(System.Windows.Rect rect)
    {
        if (!IsSelecting)
        {
            return;
        }

        X = rect.X;
        Y = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    public void Complete(System.Windows.Rect rect)
    {
        if (!IsSelecting)
        {
            return;
        }

        Update(rect);
        IsSelecting = false;
        HasSelection = rect.Width >= 4 && rect.Height >= 4;
    }

    public void Clear()
    {
        IsSelecting = false;
        HasSelection = false;
        X = 0;
        Y = 0;
        Width = 0;
        Height = 0;
    }

    public System.Windows.Rect GetSelectionRect()
    {
        return new System.Windows.Rect(X, Y, Width, Height);
    }
}
