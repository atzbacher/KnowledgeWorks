using System;
using System.Diagnostics;
using LM.Core.Abstractions;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public sealed class LibraryDocumentService : ILibraryDocumentService
    {
        private readonly IWorkSpaceService _workspace;

        public LibraryDocumentService(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public void OpenEntry(Entry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            var relativePath = entry.MainFilePath;
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidOperationException("Entry does not have an associated document path.");

            var absolutePath = _workspace.GetAbsolutePath(relativePath);
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new InvalidOperationException("Unable to resolve document path in workspace.");

            var info = new ProcessStartInfo
            {
                FileName = absolutePath,
                UseShellExecute = true
            };

            Process.Start(info);
        }
    }
}
