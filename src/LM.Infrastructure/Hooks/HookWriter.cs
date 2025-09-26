#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.FileSystem;
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

        public async Task SaveDataExtractionAsync(string entryId, HookM.DataExtractionHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));

            var json = JsonSerializer.Serialize(hook, s_jsonOptions);
            var hash = ComputeSha256(json);
            var relative = WorkspaceLayout.DataExtractionRelativePath(hash);
            var normalizedRelative = relative.Replace(Path.DirectorySeparatorChar, '/');
            var absolute = WorkspaceLayout.DataExtractionAbsolutePath(_workspace, hash);

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllTextAsync(absolute, json, ct).ConfigureAwait(false);

            await UpdateHubDataExtractionAsync(entryId, normalizedRelative, ct).ConfigureAwait(false);
        }

        private static string ComputeSha256(string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static async Task<JsonObject?> LoadHubJsonAsync(string hubPath, CancellationToken ct)
        {
            if (!File.Exists(hubPath))
                return null;

            await using var fs = new FileStream(
                hubPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            return await JsonNode.ParseAsync(fs, cancellationToken: ct).ConfigureAwait(false) as JsonObject;
        }

        private static void EnsureHooksObject(JsonObject hub)
        {
            if (hub["hooks"] is JsonObject)
                return;

            hub["hooks"] = new JsonObject();
        }

        private static async Task PersistHubAsync(string hubPath, JsonObject hub, CancellationToken ct)
        {
            var json = hub.ToJsonString(HookM.JsonStd.Options);
            await File.WriteAllTextAsync(hubPath, json, ct).ConfigureAwait(false);
        }

        private static void SetDataExtractionPath(JsonObject hub, string normalizedRelative)
        {
            var hooksNode = hub["hooks"] as JsonObject;
            if (hooksNode is null)
            {
                hooksNode = new JsonObject();
                hub["hooks"] = hooksNode;
            }

            hooksNode["data_extraction"] = normalizedRelative;
        }

        private async Task UpdateHubDataExtractionAsync(string entryId, string normalizedRelative, CancellationToken ct)
        {
            var hubPath = WorkspaceLayout.HubPath(_workspace, entryId);
            var hub = await LoadHubJsonAsync(hubPath, ct).ConfigureAwait(false);
            if (hub is null)
                return;

            EnsureHooksObject(hub);
            SetDataExtractionPath(hub, normalizedRelative);
            await PersistHubAsync(hubPath, hub, ct).ConfigureAwait(false);
        }
    }
}
