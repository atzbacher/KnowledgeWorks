#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library;

public sealed partial class PdfPageThumbnailViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isCurrent;

    public PdfPageThumbnailViewModel(int pageNumber, System.Windows.Media.ImageSource thumbnail)
    {
        PageNumber = pageNumber;
        Thumbnail = thumbnail ?? throw new System.ArgumentNullException(nameof(thumbnail));
    }

    public int PageNumber { get; }

    public System.Windows.Media.ImageSource Thumbnail { get; }
}
