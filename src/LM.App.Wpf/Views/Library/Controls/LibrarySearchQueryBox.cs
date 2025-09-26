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
        private static readonly System.Windows.Media.Brush KeywordForegroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        private static readonly System.Windows.Media.Brush KeywordBackgroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(16, 76, 121));
        private static readonly System.Windows.Media.Brush OperatorForegroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        private static readonly System.Windows.Media.Brush OperatorBackgroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(55, 118, 54));
        private static readonly System.Windows.Media.Brush ParenthesisGlyphForegroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        private static readonly ParenthesisPalette[] ParenthesisPalettes =
        {
            new ParenthesisPalette(
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(16, 76, 121)),
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(227, 238, 247))),
            new ParenthesisPalette(
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(55, 118, 54)),
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(230, 244, 228))),
            new ParenthesisPalette(
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(121, 76, 16)),
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(246, 237, 225))),
            new ParenthesisPalette(
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(94, 55, 118)),
                CreateFrozenBrush(System.Windows.Media.Color.FromRgb(239, 230, 245)))
        };
        private static readonly ParenthesisPalette ParenthesisFallbackPalette = new(
            CreateFrozenBrush(System.Windows.Media.Color.FromRgb(120, 120, 120)),
            CreateFrozenBrush(System.Windows.Media.Color.FromRgb(234, 234, 234)));
        private static readonly System.Windows.Media.Brush AccentBorderBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(196, 202, 208));
        private static readonly System.Windows.Media.Brush AccentBackgroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(236, 239, 242));
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
            BorderBrush = AccentBorderBrush;
            Background = AccentBackgroundBrush;
            Padding = new System.Windows.Thickness(10, 6, 10, 6);
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
                inlines.Add(CreateInline(string.Empty, Foreground, null, System.Windows.FontWeights.Normal));
                return;
            }

            var highlights = BuildHighlightSegments(text);
            var index = 0;

            foreach (var segment in highlights)
            {
                if (segment.Start > index)
                {
                    var plain = text.Substring(index, segment.Start - index);
                    inlines.Add(CreateInline(plain, Foreground, null, System.Windows.FontWeights.Normal));
                }

                var highlightText = text.Substring(segment.Start, segment.Length);
                inlines.Add(CreateInline(highlightText, segment.Foreground, segment.Background, segment.FontWeight));
                index = segment.Start + segment.Length;
            }

            if (index < text.Length)
            {
                var remainder = text.Substring(index);
                inlines.Add(CreateInline(remainder, Foreground, null, System.Windows.FontWeights.Normal));
            }
        }

        private HighlightSegment[] BuildHighlightSegments(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<HighlightSegment>();
            }

            var segments = new List<HighlightSegment>();
            var occupied = new bool[text.Length];

            AddKeywordSegments(text, segments, occupied);
            AddOperatorSegments(text, segments, occupied);
            AddParenthesisSegments(text, segments, occupied);
            AddParenthesisDepthSegments(text, segments, occupied);

            if (segments.Count == 0)
            {
                return Array.Empty<HighlightSegment>();
            }

            segments.Sort(static (a, b) => a.Start.CompareTo(b.Start));

            return segments.ToArray();
        }

        private void AddKeywordSegments(string text, List<HighlightSegment> segments, bool[] occupied)
        {
            foreach (Match match in KeywordRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                var length = match.Length;
                var colonIndex = match.Index + match.Length;
                while (colonIndex < text.Length && char.IsWhiteSpace(text[colonIndex]))
                {
                    colonIndex++;
                }
                if (colonIndex < text.Length && text[colonIndex] == ':')
                {
                    length = colonIndex - match.Index + 1;
                }

                AddSegmentIfAvailable(segments, occupied, match.Index, length, KeywordForegroundBrush, KeywordBackgroundBrush, System.Windows.FontWeights.SemiBold);
            }
        }

        private void AddOperatorSegments(string text, List<HighlightSegment> segments, bool[] occupied)
        {
            foreach (Match match in OperatorRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                AddSegmentIfAvailable(segments, occupied, match.Index, match.Length, OperatorForegroundBrush, OperatorBackgroundBrush, System.Windows.FontWeights.SemiBold);
            }
        }

        private void AddParenthesisSegments(string text, List<HighlightSegment> segments, bool[] occupied)
        {
            var paletteStack = new Stack<ParenthesisPalette>();

            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];

                if (character == '(')
                {
                    var palette = ParenthesisPalettes[paletteStack.Count % ParenthesisPalettes.Length];
                    paletteStack.Push(palette);
                    AddSegmentIfAvailable(segments, occupied, i, 1, ParenthesisGlyphForegroundBrush, palette.GlyphBackground, System.Windows.FontWeights.SemiBold);
                }
                else if (character == ')')
                {
                    var palette = paletteStack.Count > 0 ? paletteStack.Pop() : ParenthesisFallbackPalette;
                    AddSegmentIfAvailable(segments, occupied, i, 1, ParenthesisGlyphForegroundBrush, palette.GlyphBackground, System.Windows.FontWeights.SemiBold);
                }
            }
        }

        private void AddParenthesisDepthSegments(string text, List<HighlightSegment> segments, bool[] occupied)
        {
            if (text.Length == 0)
            {
                return;
            }

            var paletteStack = new Stack<ParenthesisPalette>();
            var rangeStart = -1;
            var hasPalette = false;
            ParenthesisPalette currentPalette = default;

            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];

                if (character == '(')
                {
                    FlushRange(i);
                    var openingPalette = ParenthesisPalettes[paletteStack.Count % ParenthesisPalettes.Length];
                    paletteStack.Push(openingPalette);
                    continue;
                }

                if (character == ')')
                {
                    FlushRange(i);
                    if (paletteStack.Count > 0)
                    {
                        paletteStack.Pop();
                    }
                    continue;
                }

                if (paletteStack.Count == 0 || occupied[i])
                {
                    FlushRange(i);
                    continue;
                }

                var activePalette = paletteStack.Peek();
                if (rangeStart < 0)
                {
                    rangeStart = i;
                    currentPalette = activePalette;
                    hasPalette = true;
                    continue;
                }

                if (!ReferenceEquals(currentPalette.RegionBackground, activePalette.RegionBackground))
                {
                    FlushRange(i);
                    rangeStart = i;
                    currentPalette = activePalette;
                    hasPalette = true;
                }
            }

            FlushRange(text.Length);

            void FlushRange(int exclusiveEnd)
            {
                if (rangeStart < 0 || !hasPalette)
                {
                    rangeStart = -1;
                    hasPalette = false;
                    return;
                }

                var length = exclusiveEnd - rangeStart;
                if (length > 0)
                {
                    AddSegmentIfAvailable(segments, occupied, rangeStart, length, null, currentPalette.RegionBackground, System.Windows.FontWeights.Normal);
                }

                rangeStart = -1;
                hasPalette = false;
            }
        }

        private void AddSegmentIfAvailable(List<HighlightSegment> segments, bool[] occupied, int start, int length, System.Windows.Media.Brush? foreground, System.Windows.Media.Brush? background, System.Windows.FontWeight fontWeight)
        {
            if (length <= 0 || start < 0 || start >= occupied.Length)
            {
                return;
            }

            var end = Math.Min(start + length, occupied.Length);
            for (var i = start; i < end; i++)
            {
                if (occupied[i])
                {
                    return;
                }
            }

            segments.Add(new HighlightSegment(start, end - start, foreground, background, fontWeight));

            for (var i = start; i < end; i++)
            {
                occupied[i] = true;
            }
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
            return TrimTrailingLineBreak(raw);
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

            var range = new System.Windows.Documents.TextRange(Document.ContentStart, pointer);
            var text = range.Text ?? string.Empty;
            var normalized = TrimTrailingLineBreak(text);
            return normalized.Length;
        }

        private System.Windows.Documents.TextPointer? GetTextPointerAtOffset(int offset)
        {
            var clamped = Math.Max(0, offset);
            var navigator = Document.ContentStart;
            var remaining = clamped;

            while (navigator is not null && remaining > 0)
            {
                var context = navigator.GetPointerContext(System.Windows.Documents.LogicalDirection.Forward);
                if (context == System.Windows.Documents.TextPointerContext.Text)
                {
                    var runText = navigator.GetTextInRun(System.Windows.Documents.LogicalDirection.Forward);
                    if (string.IsNullOrEmpty(runText))
                    {
                        navigator = navigator.GetNextContextPosition(System.Windows.Documents.LogicalDirection.Forward);
                        continue;
                    }

                    if (runText.Length >= remaining)
                    {
                        return navigator.GetPositionAtOffset(remaining, System.Windows.Documents.LogicalDirection.Forward);
                    }

                    navigator = navigator.GetPositionAtOffset(runText.Length, System.Windows.Documents.LogicalDirection.Forward);
                    remaining -= runText.Length;
                }
                else
                {
                    navigator = navigator.GetNextContextPosition(System.Windows.Documents.LogicalDirection.Forward);
                }
            }
            return navigator ?? Document.ContentEnd;
        }

        private System.Windows.Documents.Inline CreateInline(string text, System.Windows.Media.Brush? foreground, System.Windows.Media.Brush? background, System.Windows.FontWeight fontWeight)
        {
            var run = new System.Windows.Documents.Run(text ?? string.Empty)
            {
                Foreground = foreground ?? Foreground,
                FontWeight = fontWeight
            };
            if (background is null)
            {
                return run;
            }

            var span = new System.Windows.Documents.Span(run)
            {
                Foreground = foreground ?? Foreground,
                Background = background,
                FontWeight = fontWeight
            };

            return span;
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

        private static string TrimTrailingLineBreak(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.EndsWith("\r\n", StringComparison.Ordinal))
            {
                return text[..^2];
            }

            if (text.Length > 0 && (text[^1] == '\r' || text[^1] == '\n'))
            {
                return text[..^1];
            }

            return text;
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
            public HighlightSegment(
                int start,
                int length,
                System.Windows.Media.Brush? foreground,
                System.Windows.Media.Brush? background,
                System.Windows.FontWeight fontWeight)
            {
                Start = start;
                Length = length;
                Foreground = foreground;
                Background = background;
                FontWeight = fontWeight;
            }

            public int Start { get; }
            public int Length { get; }
            public System.Windows.Media.Brush? Foreground { get; }
            public System.Windows.Media.Brush? Background { get; }
            public System.Windows.FontWeight FontWeight { get; }
        }

        private readonly struct ParenthesisPalette
        {
            public ParenthesisPalette(System.Windows.Media.Brush glyphBackground, System.Windows.Media.Brush regionBackground)
            {
                GlyphBackground = glyphBackground;
                RegionBackground = regionBackground;
            }

            public System.Windows.Media.Brush GlyphBackground { get; }
            public System.Windows.Media.Brush RegionBackground { get; }
        }
    }
}
