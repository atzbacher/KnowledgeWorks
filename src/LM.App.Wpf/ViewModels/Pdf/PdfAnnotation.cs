using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;

namespace LM.App.Wpf.ViewModels.Pdf
{
    /// <summary>
    /// Represents an annotation displayed in the PDF viewer sidebar.
    /// </summary>
    public sealed class PdfAnnotation : Common.ViewModelBase, IDisposable
    {
        private static readonly IReadOnlyDictionary<string, string> NamedHighlightColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["yellow"] = "#FFFF98",
            ["green"] = "#53FFBC",
            ["blue"] = "#80EBFF",
            ["pink"] = "#FFCBE6",
            ["red"] = "#FF4F5F",
            ["yellow_hcm"] = "#FFFFCC",
            ["green_hcm"] = "#53FFBC",
            ["blue_hcm"] = "#80EBFF",
            ["pink_hcm"] = "#F6B8FF",
            ["red_hcm"] = "#C50043",
        };

        private static readonly System.Windows.Media.Brush DefaultHighlightBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0xFD, 0xFD, 0xFD));

        private string _title;
        private string? _textSnippet;
        private string? _note;
        private string? _previewImagePath;
        private System.Windows.Media.Imaging.BitmapImage? _previewImage;
        private string? _colorName;
        private string? _colorHex;
        private System.Windows.Media.Brush _highlightBrush = DefaultHighlightBrush;
        private int _pageNumber;
        private string? _annotationType;
        private string? _annotationTypeDisplay;
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
                if (SetProperty(ref _colorName, sanitized))
                {
                    ColorHex = NormalizeColorHex(sanitized);
                }
            }
        }

        /// <summary>
        /// Gets or sets the normalized RGB color applied to the annotation highlight.
        /// </summary>
        public string? ColorHex
        {
            get => _colorHex;
            set
            {
                var normalized = NormalizeColorHex(value);
                if (SetProperty(ref _colorHex, normalized))
                {
                    UpdateHighlightBrush(normalized);
                }
            }
        }

        /// <summary>
        /// Gets the brush used to render the annotation background in the sidebar.
        /// </summary>
        public System.Windows.Media.Brush HighlightBrush
        {
            get => _highlightBrush;
            private set
            {
                var brush = value ?? DefaultHighlightBrush;
                SetProperty(ref _highlightBrush, brush);
            }
        }

        /// <summary>
        /// Gets or sets the raw annotation type reported by the viewer bridge.
        /// </summary>
        public string? AnnotationType
        {
            get => _annotationType;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (SetProperty(ref _annotationType, sanitized))
                {
                    AnnotationTypeDisplay = ResolveAnnotationTypeDisplay(sanitized);
                }
            }
        }

        /// <summary>
        /// Gets a friendly label describing the annotation type.
        /// </summary>
        public string? AnnotationTypeDisplay
        {
            get => _annotationTypeDisplay;
            private set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value;
                SetProperty(ref _annotationTypeDisplay, sanitized);
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

        private static string? NormalizeColorHex(string? value)
        {
            return TryNormalizeHex(value, out var normalized) ? normalized : null;
        }

        private static bool TryNormalizeHex(string? value, out string? normalized)
        {
            normalized = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();

            if (NamedHighlightColors.TryGetValue(trimmed, out var mapped))
            {
                normalized = mapped;
                return true;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            if (trimmed.Length == 8)
            {
                trimmed = trimmed[2..];
            }

            if (trimmed.Length != 6)
            {
                return false;
            }

            for (var i = 0; i < trimmed.Length; i++)
            {
                if (!IsHexDigit(trimmed[i]))
                {
                    return false;
                }
            }

            normalized = string.Concat("#", trimmed.ToUpperInvariant());
            return true;
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9')
                || (value >= 'a' && value <= 'f')
                || (value >= 'A' && value <= 'F');
        }

        private void UpdateHighlightBrush(string? normalizedHex)
        {
            if (TryParseHexColor(normalizedHex, out var color))
            {
                HighlightBrush = CreateFrozenBrush(color);
                return;
            }

            HighlightBrush = DefaultHighlightBrush;
        }

        private static bool TryParseHexColor(string? normalizedHex, out System.Windows.Media.Color color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(normalizedHex))
            {
                return false;
            }

            var trimmed = normalizedHex.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            if (trimmed.Length != 6)
            {
                return false;
            }

            if (!byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
                || !byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
                || !byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            {
                return false;
            }

            color = System.Windows.Media.Color.FromRgb(red, green, blue);
            return true;
        }

        private static System.Windows.Media.Brush CreateFrozenBrush(System.Windows.Media.Color color)
        {
            var brush = new System.Windows.Media.SolidColorBrush(color);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private static string? ResolveAnnotationTypeDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();

            if (trimmed.Equals("highlight", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("highlightEditor", StringComparison.OrdinalIgnoreCase))
            {
                return "Highlight";
            }

            if (trimmed.Equals("ink", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("inkEditor", StringComparison.OrdinalIgnoreCase))
            {
                return "Ink";
            }

            if (trimmed.Equals("freetext", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("freeTextEditor", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                return "Text";
            }

            if (trimmed.Equals("stamp", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("stampEditor", StringComparison.OrdinalIgnoreCase))
            {
                return "Stamp";
            }

            if (trimmed.Equals("signature", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("signatureEditor", StringComparison.OrdinalIgnoreCase))
            {
                return "Signature";
            }

            var baseValue = trimmed;
            if (baseValue.EndsWith("Editor", StringComparison.OrdinalIgnoreCase))
            {
                baseValue = baseValue[..^6];
            }

            baseValue = baseValue.Replace('_', ' ').Trim();
            if (baseValue.Length == 0)
            {
                return null;
            }

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(baseValue);
        }

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
