using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;

namespace LM.Infrastructure.Settings
{
    /// <summary>Persists watched folder settings beside the workspace directory.</summary>
    public sealed class JsonWatchedFolderSettingsStore : IWatchedFolderSettingsStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IWorkSpaceService _workspace;

        public JsonWatchedFolderSettingsStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<WatchedFolderSettings> LoadAsync(CancellationToken ct = default)
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return new WatchedFolderSettings();
            }

            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(stream, Options, ct).ConfigureAwait(false);
                if (snapshot is null)
                    return new WatchedFolderSettings();

                var folders = snapshot.Folders?.Select(f => new WatchedFolderSettingsFolder
                {
                    Path = f.Path ?? string.Empty,
                    IsEnabled = f.IsEnabled
                }).Where(f => !string.IsNullOrWhiteSpace(f.Path)).ToArray() ?? Array.Empty<WatchedFolderSettingsFolder>();

                var states = snapshot.States?.Select(s => new WatchedFolderState(
                    s.Path ?? string.Empty,
                    s.LastScanUtc,
                    s.AggregatedHash,
                    s.LastScanWasUnchanged)).Where(s => !string.IsNullOrWhiteSpace(s.Path)).ToArray() ?? Array.Empty<WatchedFolderState>();

                return new WatchedFolderSettings
                {
                    Folders = folders,
                    States = states
                };
            }
            catch (JsonException)
            {
                return new WatchedFolderSettings();
            }
        }

        public async Task SaveAsync(WatchedFolderSettings settings, CancellationToken ct = default)
        {
            if (settings is null)
                throw new ArgumentNullException(nameof(settings));

            var snapshot = new Snapshot
            {
                Folders = settings.Folders?.Select(f => new FolderSnapshot
                {
                    Path = f.Path,
                    IsEnabled = f.IsEnabled
                }).ToArray(),
                States = settings.States?.Select(s => new StateSnapshot
                {
                    Path = s.Path,
                    LastScanUtc = s.LastScanUtc,
                    AggregatedHash = s.AggregatedHash,
                    LastScanWasUnchanged = s.LastScanWasUnchanged
                }).ToArray()
            };

            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, snapshot, Options, ct).ConfigureAwait(false);
        }

        private string GetSettingsPath()
        {
            var root = _workspace.GetWorkspaceRoot();
            var fullRoot = Path.GetFullPath(root);
            var trimmed = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.Length == 0)
            {
                trimmed = fullRoot;
            }

            var name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name))
            {
                name = "workspace";
            }

            var directory = Path.GetDirectoryName(trimmed);
            if (string.IsNullOrEmpty(directory))
            {
                directory = fullRoot;
            }

            return Path.Combine(directory, $"{name}.watched-folders.json");
        }

        private sealed class Snapshot
        {
            public FolderSnapshot[]? Folders { get; set; }
            public StateSnapshot[]? States { get; set; }
        }

        private sealed class FolderSnapshot
        {
            public string? Path { get; set; }
            public bool IsEnabled { get; set; }
        }

        private sealed class StateSnapshot
        {
            public string? Path { get; set; }
            public DateTimeOffset? LastScanUtc { get; set; }
            public string? AggregatedHash { get; set; }
            public bool LastScanWasUnchanged { get; set; }
        }
    }
}
