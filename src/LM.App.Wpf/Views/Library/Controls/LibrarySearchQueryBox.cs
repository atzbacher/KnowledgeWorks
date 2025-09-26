#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LM.App.Wpf.Library.Search;

namespace LM.App.Wpf.Views.Library.Controls
{
    public sealed class LibrarySearchQueryBox : System.Windows.Controls.RichTextBox
    {
        private static readonly System.Windows.Media.Brush KeywordBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(72, 118, 214));
        private static readonly System.Windows.Media.Brush OperatorBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(176, 88, 35));
        private static readonly Regex KeywordRegex = BuildKeywordRegex();
        private static readonly Regex OperatorRegex = new("(?<!\\w)(AND|OR|NOT)(?!\\w)", RegexOptions.Compiled);

        private bool _suppressTextSync;
        private bool _suppressHighlight;

        internal static readonly System.Windows.DependencyProperty TextProperty = System.Windows.DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(LibrarySearchQueryBox),
            new System.Windows.FrameworkPropertyMetadata(string.Empty, System.Windows.FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

        public LibrarySearchQueryBox()
        {
            AcceptsReturn = false;
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
            BorderThickness = new System.Windows.Thickness(1);
            Padding = new System.Windows.Thickness(6, 4, 6, 4);
            Document.Blocks.Clear();
            Document.Blocks.Add(new System.Windows.Documents.Paragraph { Margin = new System.Windows.Thickness(0) });
            Document.PagePadding = new System.Windows.Thickness(0);
            System.Windows.DataObject.AddPastingHandler(this, OnPaste);
        }

        public string Text
        {
            get => (string)(GetValue(TextProperty) ?? string.Empty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnTextChanged(System.Windows.Controls.TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            if (_suppressHighlight)
            {
                return;
            }

            var current = GetDocumentText();
            if (!string.Equals(current, Text, StringComparison.Ordinal))
            {
                _suppressTextSync = true;
                SetCurrentValue(TextProperty, current);
                _suppressTextSync = false;
            }

            UpdateDocument(current);
        }

        private static void OnTextPropertyChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is not LibrarySearchQueryBox box)
            {
                return;
            }

            if (box._suppressTextSync)
            {
                return;
            }

            var text = e.NewValue as string ?? string.Empty;
            box.UpdateDocument(text);
        }

        private void UpdateDocument(string text)
        {
            _suppressHighlight = true;
            try
            {
                var selectionStart = GetCharacterOffset(Selection.Start);
                var selectionLength = GetCharacterOffset(Selection.End) - selectionStart;

                Document.Blocks.Clear();
                var paragraph = new System.Windows.Documents.Paragraph { Margin = new System.Windows.Thickness(0) };
                AppendRuns(paragraph.Inlines, text ?? string.Empty);
                Document.Blocks.Add(paragraph);

                RestoreSelection(selectionStart, selectionLength);
            }
            finally
            {
                _suppressHighlight = false;
            }
        }

        private void AppendRuns(System.Windows.Documents.InlineCollection inlines, string text)
        {
            if (inlines is null)
            {
                throw new ArgumentNullException(nameof(inlines));
            }

            inlines.Clear();

            if (string.IsNullOrEmpty(text))
            {
                inlines.Add(CreateRun(string.Empty, Foreground, System.Windows.FontWeights.Normal));
                return;
            }

            var highlights = BuildHighlightSegments(text);
            var index = 0;

            foreach (var segment in highlights)
            {
                if (segment.Start > index)
                {
                    var plain = text.Substring(index, segment.Start - index);
                    inlines.Add(CreateRun(plain, Foreground, System.Windows.FontWeights.Normal));
                }

                var highlightText = text.Substring(segment.Start, segment.Length);
                inlines.Add(CreateRun(highlightText, segment.Foreground, segment.FontWeight));
                index = segment.Start + segment.Length;
            }

            if (index < text.Length)
            {
                var remainder = text.Substring(index);
                inlines.Add(CreateRun(remainder, Foreground, System.Windows.FontWeights.Normal));
            }
        }

        private HighlightSegment[] BuildHighlightSegments(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<HighlightSegment>();
            }

            var segments = new List<HighlightSegment>();

            foreach (Match match in KeywordRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                var length = match.Length;
                var colonIndex = match.Index + match.Length;
                if (colonIndex < text.Length && text[colonIndex] == ':')
                {
                    length += 1;
                }

                segments.Add(new HighlightSegment(match.Index, length, KeywordBrush, System.Windows.FontWeights.SemiBold));


            }

            foreach (Match match in OperatorRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                segments.Add(new HighlightSegment(match.Index, match.Length, OperatorBrush, System.Windows.FontWeights.SemiBold));
            }

            if (segments.Count == 0)
            {
                return Array.Empty<HighlightSegment>();
            }

            segments.Sort(static (a, b) =>
            {
                var startComparison = a.Start.CompareTo(b.Start);
                if (startComparison != 0)
                {
                    return startComparison;
                }

                return b.Length.CompareTo(a.Length);
            });

            var filtered = new List<HighlightSegment>(segments.Count);
            var currentEnd = -1;
            foreach (var segment in segments)
            {
                if (segment.Start >= currentEnd)
                {
                    filtered.Add(segment);
                    currentEnd = segment.Start + segment.Length;
                }
            }

            return filtered.ToArray();
        }

        private static Regex BuildKeywordRegex()
        {
            var tokens = LibrarySearchFieldMap.GetAllTokens();
            if (tokens.Count == 0)
            {
                return new Regex("$^", RegexOptions.Compiled);
            }

            var escaped = tokens.Select(static token => Regex.Escape(token));
            var pattern = $"(?<!\\w)({string.Join("|", escaped)})(?=\\s*:)";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        private string GetDocumentText()
        {
            var range = new System.Windows.Documents.TextRange(Document.ContentStart, Document.ContentEnd);
            var raw = range.Text ?? string.Empty;
            return raw.TrimEnd('\r', '\n');
        }

        private void RestoreSelection(int start, int length)
        {
            var clampedStart = Math.Max(0, start);
            var clampedEnd = Math.Max(clampedStart, start + length);
            var startPointer = GetTextPointerAtOffset(clampedStart) ?? Document.ContentStart;
            var endPointer = GetTextPointerAtOffset(clampedEnd) ?? startPointer;

            Selection.Select(startPointer, endPointer);
        }

        private int GetCharacterOffset(System.Windows.Documents.TextPointer pointer)
        {
            if (pointer is null)
            {
                return 0;
            }

            return Document.ContentStart.GetOffsetToPosition(pointer);

        }

        private System.Windows.Documents.TextPointer? GetTextPointerAtOffset(int offset)
        {

            var clamped = Math.Max(0, offset);
            return Document.ContentStart.GetPositionAtOffset(clamped, System.Windows.Documents.LogicalDirection.Forward);

        }

        private System.Windows.Documents.Run CreateRun(string text, System.Windows.Media.Brush? foreground, System.Windows.FontWeight fontWeight)
        {
            var run = new System.Windows.Documents.Run(text ?? string.Empty)
            {
                Foreground = foreground ?? Foreground,
                FontWeight = fontWeight
            };
            return run;
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

        private void OnPaste(object? sender, System.Windows.DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText, true))
            {
                e.CancelCommand();
                return;
            }

            var text = e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) as string ?? string.Empty;
            var sanitized = text.Replace('\r', ' ').Replace('\n', ' ');
            e.DataObject = new System.Windows.DataObject(System.Windows.DataFormats.UnicodeText, sanitized);
        }

        private readonly struct HighlightSegment
        {
            public HighlightSegment(int start, int length, System.Windows.Media.Brush foreground, System.Windows.FontWeight fontWeight)
            {
                Start = start;
                Length = length;
                Foreground = foreground;
                FontWeight = fontWeight;
            }

            public int Start { get; }
            public int Length { get; }
            public System.Windows.Media.Brush Foreground { get; }
            public System.Windows.FontWeight FontWeight { get; }
        }
    }
}
