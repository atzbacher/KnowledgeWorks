#nullable enable
using System;
using System.IO;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Export
{
    internal sealed class DataExtractionExportContext
    {
        private readonly IWorkSpaceService _workspace;

        public DataExtractionExportContext(string entryId,
                                           HookM.EntryHub hub,
                                           HookM.DataExtractionHook extraction,
                                           HookM.ArticleHook? article,
                                           IWorkSpaceService workspace)
        {
            EntryId = entryId ?? throw new ArgumentNullException(nameof(entryId));
            Hub = hub ?? throw new ArgumentNullException(nameof(hub));
            Extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Article = article;
        }

        public string EntryId { get; }

        public HookM.EntryHub Hub { get; }

        public HookM.DataExtractionHook Extraction { get; }

        public HookM.ArticleHook? Article { get; }

        public string? TryResolveAbsolutePath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return _workspace.GetAbsolutePath(normalized);
        }
    }
}
