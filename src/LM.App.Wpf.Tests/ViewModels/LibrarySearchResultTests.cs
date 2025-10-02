using System.Collections.Generic;
using LM.App.Wpf.ViewModels;
using LM.Core.Models;
using Xunit;

namespace LM.App.Wpf.Tests.ViewModels
{
    public sealed class LibrarySearchResultTests
    {
        [Theory]
        [InlineData("paper.pdf", LibraryAttachmentGlyph.Pdf)]
        [InlineData("draft.docx", LibraryAttachmentGlyph.Document)]
        [InlineData("slides.ppt", LibraryAttachmentGlyph.Presentation)]
        [InlineData("notes.txt", LibraryAttachmentGlyph.Generic)]
        public void PrimaryAttachmentGlyphKind_UsesMainFileWhenPresent(string path, LibraryAttachmentGlyph expected)
        {
            var entry = new Entry
            {
                MainFilePath = path
            };

            var result = new LibrarySearchResult(entry, null, null);

            Assert.Equal(expected, result.PrimaryAttachmentGlyphKind);
        }

        [Theory]
        [InlineData("supplement.pdf", LibraryAttachmentGlyph.Pdf)]
        [InlineData("summary.doc", LibraryAttachmentGlyph.Document)]
        [InlineData("deck.pptx", LibraryAttachmentGlyph.Presentation)]
        [InlineData("readme.md", LibraryAttachmentGlyph.Generic)]
        public void PrimaryAttachmentGlyphKind_FallsBackToAttachments(string relativePath, LibraryAttachmentGlyph expected)
        {
            var entry = new Entry
            {
                MainFilePath = string.Empty,
                Attachments = new List<Attachment>
                {
                    new Attachment { RelativePath = relativePath }
                }
            };

            var result = new LibrarySearchResult(entry, null, null);

            Assert.Equal(expected, result.PrimaryAttachmentGlyphKind);
        }

        [Fact]
        public void PrimaryAttachmentGlyphKind_ReturnsNoneWhenNoFiles()
        {
            var entry = new Entry();

            var result = new LibrarySearchResult(entry, null, null);

            Assert.Equal(LibraryAttachmentGlyph.None, result.PrimaryAttachmentGlyphKind);
            Assert.False(result.HasPrimaryAttachment);
        }
    }
}

