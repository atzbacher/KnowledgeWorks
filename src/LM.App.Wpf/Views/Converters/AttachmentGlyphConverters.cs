using System;
using System.Globalization;
using System.Windows.Data;

namespace LM.App.Wpf.Views.Converters
{
    public sealed class AttachmentGlyphToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not LM.App.Wpf.ViewModels.LibraryAttachmentGlyph glyph)
            {
                return null;
            }

            return glyph switch
            {
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Pdf => "\uE7B8", // PDF icon
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Document => "\uE8A5", // Document icon
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Presentation => "\uE7C0", // Presentation icon
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Generic => "\uE16C", // Paperclip
                _ => null
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("AttachmentGlyphToIconConverter does not support ConvertBack.");
        }
    }

    public sealed class AttachmentGlyphToDescriptionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not LM.App.Wpf.ViewModels.LibraryAttachmentGlyph glyph)
            {
                return null;
            }

            return glyph switch
            {
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Pdf => "Primary attachment: PDF",
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Document => "Primary attachment: document",
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Presentation => "Primary attachment: presentation",
                LM.App.Wpf.ViewModels.LibraryAttachmentGlyph.Generic => "Primary attachment: file",
                _ => null
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("AttachmentGlyphToDescriptionConverter does not support ConvertBack.");
        }
    }
}

