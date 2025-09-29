#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Collections;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using PdfiumViewer;
using PdfiumViewer.Enums;
using UglyToad.PdfPig.Outline;

namespace LM.App.Wpf.ViewModels.Library;

public enum PdfViewerPaneTab
{
    Thumbnails,
    Annotations,
    Outline
}

public enum PdfAnnotationTool
{
    None,
    Highlight,
    Underline,
    Rectangle,
    Note
}

public sealed partial class PdfViewerViewModel : ViewModelBase
{
    private const double DefaultSidePaneWidth = 320;

    private readonly IUserPreferencesStore _preferencesStore;
    private readonly ObservableCollection<PdfPageThumbnailViewModel> _thumbnails = new();
    private readonly ObservableCollection<PdfAnnotationListItemViewModel> _annotations = new();
    private readonly ObservableCollection<PdfOutlineNodeViewModel> _outlineNodes = new();

    private Entry? _entry;
    private Attachment? _attachment;
    private string? _documentPath;
    private bool _isSidePaneVisible = true;
    private System.Windows.GridLength _sidePaneWidth = new System.Windows.GridLength(DefaultSidePaneWidth);
    private PdfViewerPaneTab _selectedPaneTab = PdfViewerPaneTab.Thumbnails;
    private PdfAnnotationTool _activeAnnotationTool = PdfAnnotationTool.Highlight;
    private PdfPageThumbnailViewModel? _selectedThumbnail;
    private PdfAnnotationListItemViewModel? _selectedAnnotation;
    private PdfOutlineNodeViewModel? _selectedOutline;
    private int _currentPageNumber = 1;
    private string _searchText = string.Empty;
    private bool _matchCase;
    private bool _matchWholeWord;
    private IList? _searchMatches;
    private string _searchSignature = string.Empty;
    private int _searchMatchIndex = -1;
    private bool _suppressThumbnailSync;
    private IPdfViewerSurface? _surface;
    private UserPreferences _preferences = new();

    public event EventHandler? SearchRequested;

    public PdfViewerViewModel(IUserPreferencesStore preferencesStore)
    {
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));

        Thumbnails = new ReadOnlyObservableCollection<PdfPageThumbnailViewModel>(_thumbnails);
        AnnotationItems = new ReadOnlyObservableCollection<PdfAnnotationListItemViewModel>(_annotations);
        OutlineItems = new ReadOnlyObservableCollection<PdfOutlineNodeViewModel>(_outlineNodes);

        _ = LoadPreferencesAsync();
    }

    public Entry? Entry => _entry;

    public Attachment? Attachment => _attachment;

    public string? DocumentPath
    {
        get => _documentPath;
        private set
        {
            if (SetProperty(ref _documentPath, value))
            {
                ResetSearchState();
            }
        }
    }

    public string WindowTitle
    {
        get
        {
            if (_entry is null)
            {
                return "PDF Viewer";
            }

            if (_attachment is not null && !string.IsNullOrWhiteSpace(_attachment.Title))
            {
                return string.IsNullOrWhiteSpace(_entry.Title)
                    ? _attachment.Title
                    : $"{_attachment.Title} — {_entry.Title}";
            }

            return string.IsNullOrWhiteSpace(_entry.Title) ? "PDF Viewer" : _entry.Title;
        }
    }

    public ReadOnlyObservableCollection<PdfPageThumbnailViewModel> Thumbnails { get; }

    public ReadOnlyObservableCollection<PdfAnnotationListItemViewModel> AnnotationItems { get; }

    public ReadOnlyObservableCollection<PdfOutlineNodeViewModel> OutlineItems { get; }

    public bool IsSidePaneVisible
    {
        get => _isSidePaneVisible;
        private set
        {
            if (SetProperty(ref _isSidePaneVisible, value))
            {
                UpdateSidePaneWidth();
                _ = PersistPreferencesAsync();
            }
        }
    }

    public System.Windows.GridLength SidePaneWidth
    {
        get => _sidePaneWidth;
        private set => SetProperty(ref _sidePaneWidth, value);
    }

    public PdfViewerPaneTab SelectedPaneTab
    {
        get => _selectedPaneTab;
        set => SetProperty(ref _selectedPaneTab, value);
    }

    public PdfAnnotationTool ActiveAnnotationTool
    {
        get => _activeAnnotationTool;
        private set => SetProperty(ref _activeAnnotationTool, value);
    }

    public PdfPageThumbnailViewModel? SelectedThumbnail
    {
        get => _selectedThumbnail;
        set
        {
            if (SetProperty(ref _selectedThumbnail, value) && value is not null && !_suppressThumbnailSync)
            {
                _surface?.TryNavigateToPage(value.PageNumber);
            }
        }
    }

    public PdfAnnotationListItemViewModel? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            if (SetProperty(ref _selectedAnnotation, value) && value is not null)
            {
                value.NavigateCommand.Execute(null);
            }
        }
    }

    public PdfOutlineNodeViewModel? SelectedOutline
    {
        get => _selectedOutline;
        set
        {
            if (SetProperty(ref _selectedOutline, value) && value is not null && value.HasDestination)
            {
                value.NavigateCommand.Execute(null);
            }
        }
    }

    public int CurrentPageNumber
    {
        get => _currentPageNumber;
        private set
        {
            if (SetProperty(ref _currentPageNumber, value))
            {
                SynchronizeThumbnailSelection(value);
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ResetSearchState();
            }
        }
    }

    public bool MatchCase
    {
        get => _matchCase;
        set
        {
            if (SetProperty(ref _matchCase, value))
            {
                ResetSearchState();
            }
        }
    }

    public bool MatchWholeWord
    {
        get => _matchWholeWord;
        set
        {
            if (SetProperty(ref _matchWholeWord, value))
            {
                ResetSearchState();
            }
        }
    }

    public void AttachSurface(IPdfViewerSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (_surface is not null)
        {
            _surface.PageChanged -= OnSurfacePageChanged;
        }

        _surface = surface;
        _surface.PageChanged += OnSurfacePageChanged;
        UpdateSidePaneWidth();
    }

    public void DetachSurface()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.PageChanged -= OnSurfacePageChanged;
        _surface = null;
    }

    public async Task<bool> InitializeAsync(Entry entry, string absolutePath, string? attachmentId)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        _entry = entry;
        _attachment = null;

        if (!string.IsNullOrWhiteSpace(attachmentId))
        {
            _attachment = entry.Attachments.FirstOrDefault(a => a.Id == attachmentId);
            if (_attachment is null)
            {
                return false;
            }
        }

        DocumentPath = absolutePath;
        OnPropertyChanged(nameof(Entry));
        OnPropertyChanged(nameof(Attachment));
        OnPropertyChanged(nameof(WindowTitle));

        await LoadDocumentArtifactsAsync(absolutePath).ConfigureAwait(true);
        SelectedPaneTab = PdfViewerPaneTab.Thumbnails;
        _ = Thumbnails.FirstOrDefault();
        return true;
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            _preferences = await _preferencesStore.LoadAsync().ConfigureAwait(true);
        }
        catch
        {
            _preferences = new UserPreferences();
        }

        _isSidePaneVisible = _preferences.Library.ShowPdfNavigationPane;
        UpdateSidePaneWidth();
        OnPropertyChanged(nameof(IsSidePaneVisible));
    }

    private async Task PersistPreferencesAsync()
    {
        var updatedLibrary = _preferences.Library with
        {
            ShowPdfNavigationPane = IsSidePaneVisible,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _preferences = _preferences with { Library = updatedLibrary };

        try
        {
            await _preferencesStore.SaveAsync(_preferences).ConfigureAwait(false);
        }
        catch
        {
            // Swallow preference persistence failures.
        }
    }

    private async Task LoadDocumentArtifactsAsync(string absolutePath)
    {
        await LoadThumbnailsAsync(absolutePath).ConfigureAwait(true);
        await LoadOutlineAsync(absolutePath).ConfigureAwait(true);

        if (_thumbnails.Count > 0)
        {
            CurrentPageNumber = 1;
            _suppressThumbnailSync = true;
            try
            {
                SelectedThumbnail = _thumbnails.FirstOrDefault();
                UpdateThumbnailSelectionState(CurrentPageNumber);
            }
            finally
            {
                _suppressThumbnailSync = false;
            }
        }
    }

    private Task LoadThumbnailsAsync(string absolutePath)
    {
        _thumbnails.Clear();

        try
        {
            var documentType = Type.GetType("PdfiumViewer.PdfDocument, PdfiumViewer");
            if (documentType is null)
            {
                return Task.CompletedTask;
            }

            var loadMethod = documentType.GetMethod("Load", new[] { typeof(string) });
            if (loadMethod is null)
            {
                return Task.CompletedTask;
            }

            dynamic? document = null;
            try
            {
                document = loadMethod.Invoke(null, new object[] { absolutePath });
                if (document is null)
                {
                    return Task.CompletedTask;
                }

                int pageCount = document.PageCount;
                for (var index = 0; index < pageCount; index++)
                {
                    dynamic pageSize = document.PageSizes[index];
                    var width = Convert.ToDouble(pageSize.Width, CultureInfo.InvariantCulture);
                    var height = Convert.ToDouble(pageSize.Height, CultureInfo.InvariantCulture);
                    var targetWidth = 160;
                    var targetHeight = (int)Math.Max(40, targetWidth * height / Math.Max(1d, width));
                    using System.Drawing.Image image = document.Render(index, targetWidth, targetHeight, 96, 96, false);
                    var bitmap = CreateBitmap(image);
                    _thumbnails.Add(new PdfPageThumbnailViewModel(index + 1, bitmap));
                }
            }
            finally
            {
                if (document is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        catch
        {
            _thumbnails.Clear();
        }

        return Task.CompletedTask;
    }

    private Task LoadOutlineAsync(string absolutePath)
    {
        _outlineNodes.Clear();

        try
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(absolutePath);
            if (document.TryGetBookmarks(out var bookmarks) && bookmarks?.Roots?.Count > 0)
            {
                foreach (var root in bookmarks.Roots)
                {
                    var node = CreateOutlineNode(root);
                    _outlineNodes.Add(node);
                }
            }
        }
        catch
        {
            _outlineNodes.Clear();
        }

        return Task.CompletedTask;
    }

    private PdfOutlineNodeViewModel CreateOutlineNode(BookmarkNode node)
    {
        int? pageNumber = null;
        string? uri = null;

        if (node is DocumentBookmarkNode documentNode)
        {
            pageNumber = documentNode.PageNumber;
        }
        else if (node is UriBookmarkNode uriNode)
        {
            uri = uriNode.Uri;
        }

        var children = new ObservableCollection<PdfOutlineNodeViewModel>();
        foreach (var child in node.Children)
        {
            children.Add(CreateOutlineNode(child));
        }

        var navigate = new CommunityToolkit.Mvvm.Input.RelayCommand(() => NavigateToOutlineDestination(pageNumber, uri));
        return new PdfOutlineNodeViewModel(node.Title, pageNumber, uri, navigate, children);
    }

    private void NavigateToOutlineDestination(int? pageNumber, string? uri)
    {
        if (pageNumber.HasValue && pageNumber.Value > 0)
        {
            _surface?.TryNavigateToPage(pageNumber.Value);
            return;
        }

        if (!string.IsNullOrWhiteSpace(uri))
        {
            TryLaunchUri(uri);
        }
    }

    private static System.Windows.Media.Imaging.BitmapImage CreateBitmap(System.Drawing.Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        using var imageStream = new MemoryStream(stream.ToArray());
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = imageStream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void OnSurfacePageChanged(object? sender, int pageNumber)
    {
        if (pageNumber <= 0)
        {
            return;
        }

        CurrentPageNumber = pageNumber;
    }

    private void SynchronizeThumbnailSelection(int pageNumber)
    {
        if (_thumbnails.Count == 0)
        {
            return;
        }

        _suppressThumbnailSync = true;
        try
        {
            var match = _thumbnails.FirstOrDefault(t => t.PageNumber == pageNumber);
            if (match is not null)
            {
                SelectedThumbnail = match;
            }
            UpdateThumbnailSelectionState(pageNumber);
        }
        finally
        {
            _suppressThumbnailSync = false;
        }
    }

    private void UpdateThumbnailSelectionState(int pageNumber)
    {
        foreach (var thumbnail in _thumbnails)
        {
            thumbnail.IsCurrent = thumbnail.PageNumber == pageNumber;
        }
    }

    private void ResetSearchState()
    {
        _searchMatches = null;
        _searchMatchIndex = -1;
        _searchSignature = string.Empty;
    }

    private void UpdateSidePaneWidth()
    {
        SidePaneWidth = _isSidePaneVisible
            ? new System.Windows.GridLength(DefaultSidePaneWidth)
            : new System.Windows.GridLength(0);
    }

    private void NavigateSearch(bool forward)
    {
        if (_surface is null || string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        var signature = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", SearchText, MatchCase, MatchWholeWord);
        if (!string.Equals(_searchSignature, signature, StringComparison.Ordinal))
        {
            _searchMatches = _surface.Search(SearchText, MatchCase, MatchWholeWord);
            _searchMatchIndex = -1;
            _searchSignature = signature;
        }

        if (_searchMatches is null || _searchMatches.Count == 0)
        {
            return;
        }

        if (forward)
        {
            _searchMatchIndex = (_searchMatchIndex + 1) % _searchMatches.Count;
        }
        else
        {
            _searchMatchIndex = (_searchMatchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        }

        var match = _searchMatches[_searchMatchIndex];
        if (match is null)
        {
            return;
        }

        _surface.ScrollIntoView(match);
    }

    private static void TryLaunchUri(string uri)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(info);
        }
        catch
        {
            // Ignore failures.
        }
    }
}
