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

        public bool HasSource => !string.IsNullOrWhiteSpace(Entry.Source);
        public bool HasNotes => !string.IsNullOrWhiteSpace(Entry.Notes);
        public bool HasUserNotes => !string.IsNullOrWhiteSpace(Entry.UserNotes);
        public bool HasInternalId => !string.IsNullOrWhiteSpace(Entry.InternalId);
        public bool HasDoi => !string.IsNullOrWhiteSpace(Entry.Doi);
        public bool HasPmid => !string.IsNullOrWhiteSpace(Entry.Pmid);
        public bool HasNct => !string.IsNullOrWhiteSpace(Entry.Nct);
        public bool HasIdentifiers => HasInternalId || HasDoi || HasPmid || HasNct;
        public bool HasLinks => Entry.Links is { Count: > 0 };
        public bool HasAttachments => Entry.Attachments is { Count: > 0 };
        public bool HasRelations => Entry.Relations is { Count: > 0 };
    }
}
