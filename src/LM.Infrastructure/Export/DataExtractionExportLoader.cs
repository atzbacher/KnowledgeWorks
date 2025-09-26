#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.FileSystem;
using LM.HubSpoke.Hubs;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Export
{
    internal sealed class DataExtractionExportLoader
    {
        private readonly IWorkSpaceService _workspace;

        public DataExtractionExportLoader(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<bool> HasExtractionAsync(string entryId, CancellationToken ct)
        {
            var context = await LoadAsync(entryId, includeExtraction: false, ct).ConfigureAwait(false);
            if (context is null)
            {
                return false;
            }

            var hookPath = context.Hub.Hooks?.DataExtraction;
            if (string.IsNullOrWhiteSpace(hookPath))
            {
                return false;
            }

            var absolute = context.TryResolveAbsolutePath(hookPath);
            return !string.IsNullOrWhiteSpace(absolute) && File.Exists(absolute);
        }

        public Task<DataExtractionExportContext?> LoadAsync(string entryId, CancellationToken ct = default)
            => LoadAsync(entryId, includeExtraction: true, ct);

        private async Task<DataExtractionExportContext?> LoadAsync(string entryId, bool includeExtraction, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return null;
            }

            var hub = await HubJsonStore.LoadAsync(_workspace, entryId, ct).ConfigureAwait(false);
            if (hub is null)
            {
                return null;
            }

            HookM.DataExtractionHook? extraction = null;
            if (includeExtraction)
            {
                var hookPath = hub.Hooks?.DataExtraction;
                if (string.IsNullOrWhiteSpace(hookPath))
                {
                    return null;
                }

                var absolute = _workspace.GetAbsolutePath(hookPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute))
                {
                    return null;
                }

                await using var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                extraction = await JsonSerializer.DeserializeAsync<HookM.DataExtractionHook>(stream, HookM.JsonStd.Options, ct).ConfigureAwait(false);
                if (extraction is null)
                {
                    return null;
                }
            }

            HookM.ArticleHook? article = null;
            var articlePath = hub.Hooks?.Article;
            if (!string.IsNullOrWhiteSpace(articlePath))
            {
                var absolute = _workspace.GetAbsolutePath(articlePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absolute))
                {
                    await using var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    article = await JsonSerializer.DeserializeAsync<HookM.ArticleHook>(stream, HookM.JsonStd.Options, ct).ConfigureAwait(false);
                }
            }

            if (includeExtraction && extraction is null)
            {
                return null;
            }

            return new DataExtractionExportContext(entryId, hub, extraction ?? new HookM.DataExtractionHook(), article, _workspace);
        }
    }
}
