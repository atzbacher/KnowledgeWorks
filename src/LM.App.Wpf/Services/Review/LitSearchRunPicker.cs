#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.Services.Review
{
    internal sealed class LitSearchRunPicker : ILitSearchRunPicker
    {
        private readonly IServiceProvider _services;
        private readonly IEntryStore _entryStore;
        private readonly IWorkSpaceService _workspace;

        public LitSearchRunPicker(
            IServiceProvider services,
            IEntryStore entryStore,
            IWorkSpaceService workspace)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _entryStore = entryStore ?? throw new ArgumentNullException(nameof(entryStore));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<LitSearchRunSelection?> PickAsync(CancellationToken cancellationToken)
        {
            var options = await LoadOptionsAsync(cancellationToken);
            if (options.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No LitSearch runs were found in the current workspace.",
                    "Select LitSearch run",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return null;
            }

            using var scope = _services.CreateScope();
            var window = scope.ServiceProvider.GetRequiredService<Views.Review.LitSearchRunPickerWindow>();
            var viewModel = window.ViewModel;
            viewModel.Initialize(options);

            var owner = System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(static w => w.IsActive);
            if (owner is not null)
            {
                window.Owner = owner;
            }

            var result = window.ShowDialog();
            if (result == true)
            {
                return viewModel.BuildSelection();
            }

            return null;
        }

        private async Task<IReadOnlyList<LitSearchRunOption>> LoadOptionsAsync(CancellationToken cancellationToken)
        {
            var options = new List<LitSearchRunOption>();
            var workspaceRoot = _workspace.GetWorkspaceRoot();

            await foreach (var entry in _entryStore.EnumerateAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsLitSearchEntry(entry) || string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                var hookAbsolutePath = ResolveHookAbsolutePath(workspaceRoot, entry);
                if (!string.IsNullOrWhiteSpace(hookAbsolutePath) && !File.Exists(hookAbsolutePath))
                {
                    hookAbsolutePath = null;
                }

                var hookRelativePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(hookAbsolutePath))
                {
                    try
                    {
                        hookRelativePath = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, hookAbsolutePath!));
                    }
                    catch (Exception)
                    {
                        hookRelativePath = string.Empty;
                    }
                }

                var sidecars = await LoadRunSidecarsAsync(workspaceRoot, entry, cancellationToken).ConfigureAwait(false);

                LitSearchHook? hook = null;
                var runs = new List<LitSearchRunOptionRun>();

                if (!string.IsNullOrWhiteSpace(hookAbsolutePath))
                {
                    hook = await TryReadHookAsync(hookAbsolutePath!, cancellationToken).ConfigureAwait(false);
                    if (hook is not null)
                    {
                        runs.AddRange(BuildRunsFromHook(hook, workspaceRoot, sidecars));
                    }
                }

                if (runs.Count == 0 && sidecars.Count > 0)
                {
                    runs.AddRange(BuildRunsFromSidecars(sidecars.Values));
                }

                if (runs.Count == 0)
                {
                    continue;
                }

                var label = hook is not null ? ResolveLabel(entry, hook) : ResolveFallbackLabel(entry);
                var query = hook?.Query ?? string.Empty;

                options.Add(new LitSearchRunOption(
                    entry.Id,
                    label,
                    query,
                    hookAbsolutePath ?? string.Empty,
                    hookRelativePath,
                    runs.OrderByDescending(static run => run.RunUtc).ToList()));
            }

            options.Sort(static (left, right) => string.Compare(left.Label, right.Label, true, CultureInfo.CurrentCulture));
            return options;
        }

        private static async Task<LitSearchHook?> TryReadHookAsync(string hookAbsolutePath, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await TryOpenHookStreamAsync(hookAbsolutePath, cancellationToken).ConfigureAwait(false);
                if (stream is null)
                {
                    return null;
                }

                return await JsonSerializer.DeserializeAsync<LitSearchHook>(stream, JsonStd.Options, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static async Task<FileStream?> TryOpenHookStreamAsync(string path, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        useAsync: true);
                }
                catch (IOException) when (await HandleRetryAsync(attempt, maxAttempts, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }
                catch (UnauthorizedAccessException) when (await HandleRetryAsync(attempt, maxAttempts, cancellationToken).ConfigureAwait(false))
t
                {
                    continue;
                }
            }

            return null;
        }

        private static async Task<bool> HandleRetryAsync(int attempt, int maxAttempts, CancellationToken cancellationToken)
        {
            if (attempt + 1 >= maxAttempts)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<Dictionary<string, RunSidecarInfo>> LoadRunSidecarsAsync(
            string workspaceRoot,
            Entry entry,
            CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, RunSidecarInfo>(StringComparer.OrdinalIgnoreCase);
            var entryId = entry.Id;
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return results;
            }

            var hooksDir = Path.Combine(workspaceRoot, "entries", entryId, "hooks");
            if (!Directory.Exists(hooksDir))
            {
                return results;
            }

            foreach (var file in Directory.EnumerateFiles(hooksDir, "litsearch_run_*_checked.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var label = hook is not null ? ResolveLabel(entry, hook) : ResolveFallbackLabel(entry);
                var query = hook?.Query ?? string.Empty;

                options.Add(new LitSearchRunOption(
                    entry.Id,
                    label,
                    query,
                    hookAbsolutePath ?? string.Empty,
                    hookRelativePath,
                    runs.OrderByDescending(static run => run.RunUtc).ToList()));
            }

            options.Sort(static (left, right) => string.Compare(left.Label, right.Label, true, CultureInfo.CurrentCulture));
            return options;
        }

        private static async Task<LitSearchHook?> TryReadHookAsync(string hookAbsolutePath, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await TryOpenHookStreamAsync(hookAbsolutePath, cancellationToken).ConfigureAwait(false);
                if (stream is null)
                {
                    return null;
                }

                return await JsonSerializer.DeserializeAsync<LitSearchHook>(stream, JsonStd.Options, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static async Task<FileStream?> TryOpenHookStreamAsync(string path, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {

                    await using var stream = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        useAsync: true);

                    var sidecar = await JsonSerializer.DeserializeAsync<CheckedEntryIdsSidecar>(stream, JsonStd.Options, cancellationToken)
                        .ConfigureAwait(false);
                    if (sidecar is null || string.IsNullOrWhiteSpace(sidecar.RunId))

                    {
                        continue;
                    }

                    var entryIds = ExtractEntryIds(sidecar.CheckedEntries?.EntryIds);
                    if (entryIds.Count == 0)
                    {
                        continue;
                    }


                    var savedUtc = sidecar.SavedUtc;
                    if (savedUtc == default)
                    {
                        savedUtc = File.GetLastWriteTimeUtc(file);
                    }

                    if (savedUtc.Kind != DateTimeKind.Utc)
                    {
                        savedUtc = DateTime.SpecifyKind(savedUtc, DateTimeKind.Utc);
                    }


                    string relativePath;
                    try
                    {
                        relativePath = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, file));
                    }
                    catch (Exception)
                    {
                        relativePath = NormalizeRelativePath(file);
                    }

                    var info = new RunSidecarInfo(
                        sidecar.RunId,
                        savedUtc,
                        entryIds,
                        file,
                        relativePath);

                    results[sidecar.RunId] = info;
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    // Ignore locked or malformed sidecar files.
                }
            }

            if (results.Count == 0)
            {
                return Array.Empty<string>();
            }

            return results;
        }

        private static IEnumerable<LitSearchRunOptionRun> BuildRunsFromHook(
            LitSearchHook hook,
            string workspaceRoot,
            IDictionary<string, RunSidecarInfo> sidecars)
        {
            var runs = new List<LitSearchRunOptionRun>();
            if (hook.Runs is null || hook.Runs.Count == 0)
            {
                return runs;
            }

            foreach (var run in hook.Runs.OrderByDescending(static r => r.RunUtc))
            {
                if (string.IsNullOrWhiteSpace(run.RunId))
                {
                    continue;
                }

                var (absolute, relative) = ResolveCheckedEntriesPaths(workspaceRoot, run.CheckedEntryIdsPath);
                IReadOnlyList<string> entryIds = Array.Empty<string>();

                if (sidecars.TryGetValue(run.RunId, out var sidecar))
                {
                    entryIds = sidecar.EntryIds;

                    absolute ??= sidecar.AbsolutePath;
                    relative ??= sidecar.RelativePath;
                    sidecars.Remove(run.RunId);
                }

                if (absolute is null)
                {
                    continue;
                }

                var entryCount = entryIds.Count;

                var totalHits = run.TotalHits > 0 ? run.TotalHits : entryCount;
                runs.Add(new LitSearchRunOptionRun(
                    run.RunId,
                    run.RunUtc,
                    totalHits,
                    run.ExecutedBy,
                    run.IsFavorite,
                    absolute,
                    relative,
                    entryIds));

            }

            return runs;
        }

        private static IEnumerable<LitSearchRunOptionRun> BuildRunsFromSidecars(IEnumerable<RunSidecarInfo> sidecars)
        {
            return sidecars
                .Where(static info => !string.IsNullOrWhiteSpace(info.AbsolutePath))
                .OrderByDescending(static info => info.SavedUtc)
                .Select(static info => new LitSearchRunOptionRun(
                    info.RunId,
                    info.SavedUtc,
                    info.EntryIds.Count,
                    null,
                    false,
                    info.AbsolutePath,
                    info.RelativePath,
                    info.EntryIds));
        }

        private static bool IsLitSearchEntry(Entry entry)
        {
            if (entry.Type == EntryType.LitSearch)
            {
                return true;
            }

            return string.Equals(entry.Source, "LitSearch", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveHookAbsolutePath(string workspaceRoot, Entry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.MainFilePath))
            {
                var candidate = ExpandWorkspacePath(workspaceRoot, entry.MainFilePath);
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var fallback = Path.Combine(workspaceRoot, "entries", entry.Id, "hooks", "litsearch.json");
            return File.Exists(fallback) ? fallback : null;
        }

        private static string? ExpandWorkspacePath(string workspaceRoot, string relativePath)
        {
            var trimmed = relativePath.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            if (Path.IsPathRooted(trimmed))
            {
                return trimmed;
            }

            var normalized = trimmed.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(workspaceRoot, normalized);
        }

        private static string ResolveLabel(Entry entry, LitSearchHook hook)
        {
            if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                return entry.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                return entry.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(hook.Title))
            {
                return hook.Title.Trim();
            }

            return entry.Id;
        }

        private static string NormalizeRelativePath(string? relativePath)
        {
            return string.IsNullOrWhiteSpace(relativePath)
                ? string.Empty
                : relativePath.Replace('\\', '/');
        }

        private static string ResolveFallbackLabel(Entry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                return entry.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                return entry.Title.Trim();
            }

            return entry.Id;
        }

        private static (string? AbsolutePath, string? RelativePath) ResolveCheckedEntriesPaths(string workspaceRoot, string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return (null, null);
            }

            string absolute;
            if (Path.IsPathRooted(storedPath))
            {
                absolute = storedPath;
            }
            else
            {
                var normalized = storedPath.Replace('/', Path.DirectorySeparatorChar);
                absolute = Path.Combine(workspaceRoot, normalized);
            }

            if (!File.Exists(absolute))
            {
                return (null, null);
            }

            string relative;
            try
            {
                relative = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, absolute));
            }
            catch (Exception)
            {
                relative = NormalizeRelativePath(storedPath);
            }

            return (absolute, relative);
        }

        private static IReadOnlyList<string> ExtractEntryIds(IReadOnlyList<string>? entryIds)
        {
            if (entryIds is null || entryIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            var results = new List<string>(entryIds.Count);
            foreach (var id in entryIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var trimmed = id.Trim();
                if (trimmed.Length > 0)
                {
                    results.Add(trimmed);
                }
            }

            if (results.Count == 0)
            {
                return Array.Empty<string>();
            }

            return results;
        }

        private sealed record RunSidecarInfo(
            string RunId,
            DateTime SavedUtc,
            IReadOnlyList<string> EntryIds,

            string AbsolutePath,
            string RelativePath);

        private sealed record CheckedEntryIdsSidecar
        {
            [JsonPropertyName("runId")]
            public string RunId { get; init; } = string.Empty;

            [JsonPropertyName("savedUtc")]
            [JsonConverter(typeof(UtcDateTimeConverter))]
            public DateTime SavedUtc { get; init; }
                = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

            [JsonPropertyName("checkedEntries")]
            public CheckedEntriesPayload CheckedEntries { get; init; } = new();
        }

        private sealed record CheckedEntriesPayload
        {
            [JsonPropertyName("entryIds")]
            public List<string> EntryIds { get; init; } = new();
        }
    }
}
