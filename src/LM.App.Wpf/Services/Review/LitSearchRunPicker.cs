#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                if (!IsLitSearchEntry(entry))
                {
                    continue;
                }

                var hookAbsolutePath = ResolveHookAbsolutePath(workspaceRoot, entry);
                if (hookAbsolutePath is null || !File.Exists(hookAbsolutePath))
                {
                    continue;
                }

                try
                {
                    await using var stream = File.OpenRead(hookAbsolutePath);
                    var hook = await JsonSerializer.DeserializeAsync<LitSearchHook>(stream, JsonStd.Options, cancellationToken)
                        .ConfigureAwait(false);
                    if (hook?.Runs is null || hook.Runs.Count == 0)
                    {
                        continue;
                    }

                    var runs = hook.Runs
                        .OrderByDescending(static r => r.RunUtc)
                        .Select(static run => new LitSearchRunOptionRun(
                            run.RunId,
                            run.RunUtc,
                            run.TotalHits,
                            run.ExecutedBy,
                            run.IsFavorite))
                        .ToList();

                    var label = ResolveLabel(entry, hook);
                    var relative = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, hookAbsolutePath));
                    options.Add(new LitSearchRunOption(
                        entry.Id,
                        label,
                        hook.Query ?? string.Empty,
                        hookAbsolutePath,
                        relative,
                        runs));
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    // Swallow malformed hooks so the picker can proceed with the remaining entries.
                }
            }

            options.Sort(static (left, right) => string.Compare(left.Label, right.Label, true, CultureInfo.CurrentCulture));
            return options;
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

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }
    }
}
