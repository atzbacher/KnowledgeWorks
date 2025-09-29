using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public sealed class LibraryDocumentService : ILibraryDocumentService
    {
        private readonly IWorkSpaceService _workspace;
        private readonly IPdfViewerLauncher _pdfViewerLauncher;

        public LibraryDocumentService(IWorkSpaceService workspace, IPdfViewerLauncher pdfViewerLauncher)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(pdfViewerLauncher);

            _workspace = workspace;
            _pdfViewerLauncher = pdfViewerLauncher;
        }

        public async Task OpenEntryAsync(Entry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var relativePath = entry.MainFilePath;
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidOperationException("Entry does not have an associated document path.");

            var absolutePath = _workspace.GetAbsolutePath(relativePath);
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new InvalidOperationException("Unable to resolve document path in workspace.");

            if (await _pdfViewerLauncher.LaunchAsync(entry).ConfigureAwait(true))
            {
                return;
            }

            LaunchWithShell(absolutePath);
        }

        public async Task OpenAttachmentAsync(Entry entry, Attachment attachment)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(attachment);

            var relativePath = attachment.RelativePath;
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidOperationException("Attachment does not have an associated document path.");

            var absolutePath = _workspace.GetAbsolutePath(relativePath);
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new InvalidOperationException("Unable to resolve attachment path in workspace.");

            if (await _pdfViewerLauncher.LaunchAsync(entry, attachment.Id).ConfigureAwait(true))
            {
                return;
            }

            LaunchWithShell(absolutePath);
        }

        private static void LaunchWithShell(string absolutePath)
        {
            var info = new ProcessStartInfo
            {
                FileName = absolutePath,
                UseShellExecute = true
            };

            Process.Start(info);
        }
    }
}
