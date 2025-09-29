using System;
using LM.App.Wpf.Common;

namespace LM.App.Wpf.ViewModels.Pdf
{
    /// <summary>
    /// Represents an annotation displayed in the PDF viewer sidebar.
    /// </summary>
    internal sealed class PdfAnnotationViewModel : ViewModelBase
    {
        private string _title;
        private string? _content;
        private string? _previewImagePath;

        public PdfAnnotationViewModel(string id, string title)
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
        /// Gets or sets the optional annotation body text.
        /// </summary>
        public string? Content
        {
            get => _content;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                SetProperty(ref _content, sanitized);
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
    }
}
