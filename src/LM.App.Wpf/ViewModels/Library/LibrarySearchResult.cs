using System;
using System.Globalization;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    public sealed class LibrarySearchResult
    {
        public LibrarySearchResult(Entry entry, double? score, string? highlight)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Score = score;
            Highlight = string.IsNullOrWhiteSpace(highlight) ? null : highlight.Trim();
        }

        public Entry Entry { get; }

        public double? Score { get; }

        public string? Highlight { get; }

        public bool IsFullText => Score.HasValue;

        public string? ScoreDisplay => Score.HasValue
            ? Score.Value.ToString("0.000", CultureInfo.InvariantCulture)
            : null;

        public string? HighlightDisplay => string.IsNullOrWhiteSpace(Highlight) ? null : Highlight;
    }
}
