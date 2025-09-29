#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library;

internal enum PdfAnnotationKind
{
    Highlight,
    Note,
    Rectangle,
    Underline
}

internal sealed partial class PdfAnnotationViewModel : ObservableObject
{
    private string? _title;
    private ObservableCollection<string> _tagCollection;
    private string? _colorKey;
    private System.Windows.Media.Brush? _colorBrush;
    private string? _meaning;
    private string? _createdBy;
    private DateTime? _lastModifiedUtc;

    public PdfAnnotationViewModel(PdfAnnotationKind kind,
                                  int pageNumber,
                                  RectangleF pdfBounds,
                                  string? note,
                                  DateTime createdAt,
                                  Guid? annotationId = null,
                                  string? title = null,
                                  IEnumerable<string>? tags = null,
                                  string? colorKey = null,
                                  System.Windows.Media.Brush? colorBrush = null,
                                  string? meaning = null,
                                  string? createdBy = null,
                                  DateTime? lastModifiedUtc = null)
    {
        Kind = kind;
        PageNumber = pageNumber;
        PdfBounds = pdfBounds;
        Note = note;
        CreatedAt = createdAt;
        AnnotationId = annotationId ?? Guid.NewGuid();
        _title = title;
        _tagCollection = new ObservableCollection<string>(tags ?? Array.Empty<string>());
        _colorKey = colorKey;
        _colorBrush = colorBrush;
        _meaning = meaning;
        _createdBy = createdBy;
        _lastModifiedUtc = lastModifiedUtc;
    }

    public Guid AnnotationId { get; }

    public PdfAnnotationKind Kind { get; }

    public int PageNumber { get; }

    public RectangleF PdfBounds { get; }

    public string? Note { get; }

    public DateTime CreatedAt { get; }

    public string? Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ObservableCollection<string> TagCollection
    {
        get => _tagCollection;
        set => SetProperty(ref _tagCollection, value ?? new ObservableCollection<string>());
    }

    public string? ColorKey
    {
        get => _colorKey;
        set => SetProperty(ref _colorKey, value);
    }

    public System.Windows.Media.Brush? ColorBrush
    {
        get => _colorBrush;
        set => SetProperty(ref _colorBrush, value);
    }

    public string? Meaning
    {
        get => _meaning;
        set => SetProperty(ref _meaning, value);
    }

    public string? CreatedBy
    {
        get => _createdBy;
        set => SetProperty(ref _createdBy, value);
    }

    public DateTime? LastModifiedUtc
    {
        get => _lastModifiedUtc;
        set => SetProperty(ref _lastModifiedUtc, value);
    }
}
