using System;
using System.Globalization;
using System.IO;
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

            PrimaryAttachmentGlyphKind = ResolvePrimaryAttachmentGlyph(entry);
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

        public LibraryAttachmentGlyph PrimaryAttachmentGlyphKind { get; }

        public int PrimaryAttachmentSortKey => PrimaryAttachmentGlyphKind switch
        {
            LibraryAttachmentGlyph.Pdf => 0,
            LibraryAttachmentGlyph.Document => 1,
            LibraryAttachmentGlyph.Presentation => 2,
            LibraryAttachmentGlyph.Generic => 3,
            _ => 4
        };

        public bool HasPrimaryAttachment => PrimaryAttachmentGlyphKind != LibraryAttachmentGlyph.None;

        public string TitleSortKey => Entry.Title ?? string.Empty;
        public int YearPresenceSortKey => Entry.Year.HasValue ? 0 : 1;
        public int YearDescendingSortKey => Entry.Year ?? int.MinValue;
        public int YearAscendingSortKey => Entry.Year ?? int.MaxValue;
        public string SourceSortKey => Entry.Source ?? string.Empty;
        public string TypeSortKey => Entry.Type.ToString();
        public DateTime AddedOnSortKey => Entry.AddedOnUtc;
        public string AddedBySortKey => Entry.AddedBy ?? string.Empty;
        public string InternalIdSortKey => Entry.InternalId ?? string.Empty;
        public string DoiSortKey => Entry.Doi ?? string.Empty;
        public string PmidSortKey => Entry.Pmid ?? string.Empty;
        public string NctSortKey => Entry.Nct ?? string.Empty;
        public string IdSortKey => Entry.Id ?? string.Empty;
        public string AuthorsSortKey => Entry.Authors is { Count: > 0 } ? string.Join(", ", Entry.Authors) : string.Empty;
        public string TagsSortKey => Entry.Tags is { Count: > 0 } ? string.Join(", ", Entry.Tags) : string.Empty;
        public int IsInternalSortKey => Entry.IsInternal ? 0 : 1;
        public string SnippetSortKey => HighlightDisplay ?? string.Empty;
        public double ScoreSortKey => Score ?? double.MinValue;

        private static LibraryAttachmentGlyph ResolvePrimaryAttachmentGlyph(Entry entry)
        {
            if (entry is null)
            {
                return LibraryAttachmentGlyph.None;
            }

            if (TryClassifyPath(entry.MainFilePath, out var glyph))
            {
                return glyph;
            }

            if (entry.Attachments is not null)
            {
                foreach (var attachment in entry.Attachments)
                {
                    if (attachment is null)
                    {
                        continue;
                    }

                    if (TryClassifyPath(attachment.RelativePath, out glyph))
                    {
                        return glyph;
                    }
                }

                if (entry.Attachments.Count > 0)
                {
                    return LibraryAttachmentGlyph.Generic;
                }
            }

            return LibraryAttachmentGlyph.None;
        }

        private static bool TryClassifyPath(string? path, out LibraryAttachmentGlyph glyph)
        {
            glyph = LibraryAttachmentGlyph.None;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
            {
                glyph = LibraryAttachmentGlyph.Generic;
                return true;
            }

            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                glyph = LibraryAttachmentGlyph.Pdf;
                return true;
            }

            if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase) || extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                glyph = LibraryAttachmentGlyph.Document;
                return true;
            }

            if (extension.Equals(".ppt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
            {
                glyph = LibraryAttachmentGlyph.Presentation;
                return true;
            }

            glyph = LibraryAttachmentGlyph.Generic;
            return true;
        }
    }
}

