#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LM.App.Wpf.ViewModels.Library;

public sealed partial class PdfAnnotationListItemViewModel : ObservableObject
{
    public PdfAnnotationListItemViewModel(Guid annotationId,
                                          string title,
                                          string? tags,
                                          System.Windows.Media.Brush accentBrush,
                                          int pageNumber,
                                          IRelayCommand navigateCommand,
                                          IRelayCommand deleteCommand)
    {
        AnnotationId = annotationId;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Tags = tags;
        AccentBrush = accentBrush ?? throw new ArgumentNullException(nameof(accentBrush));
        PageNumber = pageNumber;
        NavigateCommand = navigateCommand ?? throw new ArgumentNullException(nameof(navigateCommand));
        DeleteCommand = deleteCommand ?? throw new ArgumentNullException(nameof(deleteCommand));
    }

    public Guid AnnotationId { get; }

    public string Title { get; }

    public string? Tags { get; }

    public System.Windows.Media.Brush AccentBrush { get; }

    public int PageNumber { get; }

    public IRelayCommand NavigateCommand { get; }

    public IRelayCommand DeleteCommand { get; }
}
