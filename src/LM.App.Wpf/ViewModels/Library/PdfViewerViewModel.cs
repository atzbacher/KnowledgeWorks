#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels.Library
{
    public sealed class PdfViewerViewModel : ViewModelBase
    {
        private Entry? _entry;
        private Attachment? _attachment;
        private string? _documentPath;

        public Entry? Entry => _entry;

        public Attachment? Attachment => _attachment;

        public string? DocumentPath
        {
            get => _documentPath;
            private set => SetProperty(ref _documentPath, value);
        }

        public string WindowTitle
        {
            get
            {
                if (_entry is null)
                {
                    return "PDF Viewer";
                }

                if (_attachment is not null && !string.IsNullOrWhiteSpace(_attachment.Title))
                {
                    return string.IsNullOrWhiteSpace(_entry.Title)
                        ? _attachment.Title
                        : $"{_attachment.Title} — {_entry.Title}";
                }

                return string.IsNullOrWhiteSpace(_entry.Title) ? "PDF Viewer" : _entry.Title;
            }
        }

        public Task<bool> InitializeAsync(Entry entry, string absolutePath, string? attachmentId)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return Task.FromResult(false);
            }

            _entry = entry;
            _attachment = null;

            if (!string.IsNullOrWhiteSpace(attachmentId))
            {
                _attachment = entry.Attachments.FirstOrDefault(a => a.Id == attachmentId);
                if (_attachment is null)
                {
                    return Task.FromResult(false);
                }
            }

            DocumentPath = absolutePath;
            OnPropertyChanged(nameof(Entry));
            OnPropertyChanged(nameof(Attachment));
            OnPropertyChanged(nameof(WindowTitle));

            return Task.FromResult(true);
        }
    }
}
