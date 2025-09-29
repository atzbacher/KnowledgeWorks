using System;
using System.Windows.Input;

namespace LM.App.Wpf.ViewModels.Pdf
{
    /// <summary>
    /// Represents an annotation displayed in the PDF viewer sidebar.
    /// </summary>
    public sealed class PdfAnnotation : Common.ViewModelBase, IDisposable
    {
        private string _title;
        private string? _textSnippet;
        private string? _note;
        private string? _previewImagePath;
        private System.Windows.Media.Imaging.BitmapImage? _previewImage;
        private string? _colorName;
        private int _pageNumber;
        private bool _disposed;

        public PdfAnnotation(string id, string title)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Annotation id cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Annotation title cannot be empty.", nameof(title));
            }

            Id = id.Trim();
            _title = title.Trim();
            _pageNumber = 1;
        }

        /// <summary>
        /// Gets the unique annotation identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets or sets the human-friendly title for the annotation.
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var sanitized = value.Trim();
                if (sanitized.Length == 0)
                {
                    throw new ArgumentException("Annotation title cannot be empty.", nameof(value));
                }

                SetProperty(ref _title, sanitized);
            }
        }

        /// <summary>
        /// Gets or sets the zero-based page number for this annotation.
        /// </summary>
        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Page number must be 1 or greater.");
                }

                SetProperty(ref _pageNumber, value);
            }
        }

        /// <summary>
        /// Gets or sets the snippet of text associated with the annotation selection.
        /// </summary>
        public string? TextSnippet
        {
            get => _textSnippet;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                SetProperty(ref _textSnippet, sanitized);
            }
        }

        /// <summary>
        /// Gets or sets the editable note associated with the annotation.
        /// </summary>
        public string? Note
        {
            get => _note;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                SetProperty(ref _note, sanitized);
            }
        }

        /// <summary>
        /// Gets or sets the workspace-relative path to the preview image for this annotation.
        /// </summary>
        public string? PreviewImagePath
        {
            get => _previewImagePath;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                SetProperty(ref _previewImagePath, sanitized);
            }
        }

        /// <summary>
        /// Gets or sets the preview thumbnail bitmap resolved for this annotation.
        /// </summary>
        public System.Windows.Media.Imaging.BitmapImage? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        /// <summary>
        /// Gets or sets the friendly color name applied to the annotation highlight.
        /// </summary>
        public string? ColorName
        {
            get => _colorName;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                SetProperty(ref _colorName, sanitized);
            }
        }

        /// <summary>
        /// Gets or sets the command invoked to copy this annotation to the clipboard.
        /// </summary>
        public ICommand? CopyCommand { get; set; }

        /// <summary>
        /// Gets or sets the command invoked to delete this annotation.
        /// </summary>
        public ICommand? DeleteCommand { get; set; }

        /// <summary>
        /// Gets or sets the command invoked to change the annotation color.
        /// </summary>
        public ICommand? ChangeColorCommand { get; set; }

        /// <summary>
        /// Gets or sets the command invoked to clear the annotation color.
        /// </summary>
        public ICommand? ClearColorCommand { get; set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            CopyCommand = null;
            DeleteCommand = null;
            ChangeColorCommand = null;
            ClearColorCommand = null;

            GC.SuppressFinalize(this);
        }
    }
}
