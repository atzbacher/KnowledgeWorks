#nullable enable
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LM.App.Wpf.ViewModels.Library;

public sealed partial class PdfOutlineNodeViewModel : ObservableObject
{
    public PdfOutlineNodeViewModel(string title,
                                   int? pageNumber,
                                   string? targetUri,
                                   IRelayCommand navigateCommand,
                                   ObservableCollection<PdfOutlineNodeViewModel> children)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        PageNumber = pageNumber;
        TargetUri = targetUri;
        NavigateCommand = navigateCommand ?? throw new ArgumentNullException(nameof(navigateCommand));
        Children = children ?? throw new ArgumentNullException(nameof(children));
    }

    public string Title { get; }

    public int? PageNumber { get; }

    public string? TargetUri { get; }

    public ObservableCollection<PdfOutlineNodeViewModel> Children { get; }

    public IRelayCommand NavigateCommand { get; }

    public bool HasDestination => PageNumber.HasValue || !string.IsNullOrWhiteSpace(TargetUri);
}
