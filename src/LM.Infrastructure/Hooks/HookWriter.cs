#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Writes hook JSON files under entries/&lt;id&gt;/hooks/.
    /// Infrastructure concern: I/O only. Callers provide the hook object.
    /// </summary>
    internal sealed class HookWriter
    {
        private readonly IWorkSpaceService _workspace;

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true
        };

        public HookWriter(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        /// <summary>
        /// Persist the ArticleHook for a given entry id.
        /// Creates entries/&lt;entryId&gt;/hooks/article.json (directories included).
        /// </summary>
        public async Task SaveArticleAsync(string entryId, HookM.ArticleHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));

            var relDir = Path.Combine("entries", entryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "article.json");

            await using var fs = new FileStream(
                absPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await JsonSerializer.SerializeAsync(fs, hook, s_jsonOptions, ct).ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }

        public async Task SaveAttachmentsAsync(string entryId, HookM.AttachmentHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));

            var relDir = Path.Combine("entries", entryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "attachments.json");

            await using var fs = new FileStream(
                absPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await JsonSerializer.SerializeAsync(fs, hook, s_jsonOptions, ct).ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }

        public async Task AppendChangeLogAsync(string entryId, HookM.EntryChangeLogHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));
            if (hook.Events is null || hook.Events.Count == 0)
                return;

            var relDir = Path.Combine("entries", entryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "changelog.json");
            HookM.EntryChangeLogHook existing;

            if (File.Exists(absPath))
            {
                try
                {
                    await using var readStream = new FileStream(
                        absPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        useAsync: true);

                    existing = await JsonSerializer.DeserializeAsync<HookM.EntryChangeLogHook>(readStream, s_jsonOptions, ct).ConfigureAwait(false)
                               ?? new HookM.EntryChangeLogHook();
                }
                catch
                {
                    existing = new HookM.EntryChangeLogHook();
                }
            }
            else
            {
                existing = new HookM.EntryChangeLogHook();
            }

            existing.Events ??= new List<HookM.EntryChangeLogEvent>();

            foreach (var evt in hook.Events)
            {
                if (evt is not null)
                    existing.Events.Add(evt);
            }

            await using var fs = new FileStream(
                absPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await JsonSerializer.SerializeAsync(fs, existing, s_jsonOptions, ct).ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }
    }
}
