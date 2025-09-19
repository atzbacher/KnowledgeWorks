#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>Represents a single watched folder entry.</summary>
    public sealed class WatchedFolder : INotifyPropertyChanged
    {
        private string _path = string.Empty;
        private bool _isEnabled = true;
        private DateTimeOffset? _lastScanUtc;
        private bool? _lastScanWasUnchanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Path
        {
            get => _path;
            set
            {
                var normalized = value ?? string.Empty;
                if (SetProperty(ref _path, normalized))
                {
                    ResetScanState();
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        [JsonIgnore]
        public DateTimeOffset? LastScanUtc
        {
            get => _lastScanUtc;
            private set
            {
                if (SetProperty(ref _lastScanUtc, value))
                {
                    OnPropertyChanged(nameof(LastScanDisplay));
                }
            }
        }

        [JsonIgnore]
        public string LastScanDisplay => _lastScanUtc is null
            ? "Never"
            : _lastScanUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

        [JsonIgnore]
        public bool? LastScanWasUnchanged
        {
            get => _lastScanWasUnchanged;
            private set
            {
                if (SetProperty(ref _lastScanWasUnchanged, value))
                {
                    OnPropertyChanged(nameof(ScanStatusLabel));
                    OnPropertyChanged(nameof(ScanStatusToolTip));
                }
            }
        }

        [JsonIgnore]
        public string ScanStatusLabel => _lastScanWasUnchanged switch
        {
            true => "Unchanged",
            false => "Changed",
            _ => "Not scanned"
        };

        [JsonIgnore]
        public string ScanStatusToolTip => _lastScanWasUnchanged switch
        {
            true => "The last scan found no new or modified files.",
            false => "The last scan detected new or modified files.",
            _ => "This folder has not been scanned yet."
        };

        internal void ApplyState(WatchedFolderState? state)
        {
            if (state is null)
            {
                ResetScanState();
                return;
            }

            LastScanUtc = state.LastScanUtc;
            LastScanWasUnchanged = state.LastScanWasUnchanged;
        }

        public void ResetScanState()
        {
            LastScanUtc = null;
            LastScanWasUnchanged = null;
        }

        internal WatchedFolder Clone()
        {
            var clone = new WatchedFolder
            {
                Path = Path,
                IsEnabled = IsEnabled
            };

            if (LastScanUtc is not null)
            {
                clone.ApplyState(new WatchedFolderState(Path, LastScanUtc, null, LastScanWasUnchanged ?? false));
            }

            return clone;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>Collection wrapper that can persist watched folders to disk.</summary>
    public sealed class WatchedFolderConfig
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ObservableCollection<WatchedFolder> Folders { get; } = new();

        private readonly Dictionary<string, WatchedFolderState> _states = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<WatchedFolder, string> _stateKeys = new();
        private readonly object _stateGate = new();

        public static async Task<WatchedFolderConfig> LoadAsync(string path, CancellationToken ct)
        {
            var config = new WatchedFolderConfig();
            if (!File.Exists(path))
                return config;

            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var snapshot = await JsonSerializer.DeserializeAsync<WatchedFolderConfigSnapshot>(stream, JsonOptions, ct).ConfigureAwait(false);
                if (snapshot?.States is not null)
                {
                    foreach (var state in snapshot.States)
                    {
                        if (string.IsNullOrWhiteSpace(state.Path))
                            continue;

                        config.StoreStateInternal(new WatchedFolderState(state.Path,
                                                                         state.LastScanUtc,
                                                                         state.AggregatedHash,
                                                                         state.LastScanWasUnchanged));
                    }
                }

                if (snapshot?.Folders is not null)
                {
                    foreach (var folder in snapshot.Folders)
                    {
                        if (string.IsNullOrWhiteSpace(folder.Path))
                            continue;

                        var watched = new WatchedFolder
                        {
                            Path = folder.Path,
                            IsEnabled = folder.IsEnabled
                        };

                        config.Folders.Add(watched);
                        config.ApplyState(watched);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed files – treat as empty config.
            }
            return config;
        }

        public async Task SaveAsync(string path, CancellationToken ct)
        {
            WatchedFolderSnapshot[] folders;
            WatchedFolderStateSnapshot[]? states;

            lock (_stateGate)
            {
                folders = Folders.Select(static f => new WatchedFolderSnapshot
                {
                    Path = f.Path,
                    IsEnabled = f.IsEnabled
                }).ToArray();

                var stateList = new List<WatchedFolderStateSnapshot>();
                foreach (var folder in Folders)
                {
                    if (!_stateKeys.TryGetValue(folder, out var key))
                        key = NormalizePath(folder.Path);

                    if (_states.TryGetValue(key, out var state))
                    {
                        stateList.Add(new WatchedFolderStateSnapshot
                        {
                            Path = folder.Path,
                            LastScanUtc = state.LastScanUtc,
                            AggregatedHash = state.AggregatedHash,
                            LastScanWasUnchanged = state.LastScanWasUnchanged
                        });
                    }
                }

                states = stateList.Count == 0 ? null : stateList.ToArray();
            }

            var snapshot = new WatchedFolderConfigSnapshot
            {
                Folders = folders,
                States = states
            };

            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, ct).ConfigureAwait(false);
        }

        public WatchedFolderState? GetState(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var key = NormalizePath(path);
            lock (_stateGate)
            {
                return _states.TryGetValue(key, out var state) ? state : null;
            }
        }

        public WatchedFolderState? GetState(WatchedFolder folder)
        {
            if (folder is null) throw new ArgumentNullException(nameof(folder));

            lock (_stateGate)
            {
                if (_stateKeys.TryGetValue(folder, out var key) && _states.TryGetValue(key, out var state))
                    return state;

                var fallback = NormalizePath(folder.Path);
                return _states.TryGetValue(fallback, out var fallbackState) ? fallbackState : null;
            }
        }

        public void StoreState(WatchedFolder folder, WatchedFolderState state)
        {
            if (folder is null) throw new ArgumentNullException(nameof(folder));

            var normalized = NormalizePath(folder.Path);
            lock (_stateGate)
            {
                _states[normalized] = state with { Path = folder.Path };
                _stateKeys[folder] = normalized;
            }

            folder.ApplyState(state with { Path = folder.Path });
        }

        public void ApplyState(WatchedFolder folder)
        {
            if (folder is null) throw new ArgumentNullException(nameof(folder));

            WatchedFolderState? state = null;
            var key = NormalizePath(folder.Path);
            lock (_stateGate)
            {
                if (_states.TryGetValue(key, out var stored))
                {
                    state = stored;
                    _stateKeys[folder] = key;
                }
                else
                {
                    _stateKeys.Remove(folder);
                }
            }

            folder.ApplyState(state);
        }

        public void ClearState(WatchedFolder folder)
        {
            if (folder is null) throw new ArgumentNullException(nameof(folder));

            lock (_stateGate)
            {
                if (_stateKeys.TryGetValue(folder, out var key))
                {
                    _stateKeys.Remove(folder);
                    _states.Remove(key);
                }
                else
                {
                    var normalized = NormalizePath(folder.Path);
                    _states.Remove(normalized);
                }
            }

            folder.ResetScanState();
        }

        private void StoreStateInternal(WatchedFolderState state)
        {
            var key = NormalizePath(state.Path);
            lock (_stateGate)
            {
                _states[key] = state;
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                var full = System.IO.Path.GetFullPath(path);
                return full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        private sealed class WatchedFolderConfigSnapshot
        {
            public WatchedFolderSnapshot[]? Folders { get; set; }
            public WatchedFolderStateSnapshot[]? States { get; set; }
        }

        private sealed class WatchedFolderSnapshot
        {
            public string Path { get; set; } = string.Empty;
            public bool IsEnabled { get; set; } = true;
        }

        private sealed class WatchedFolderStateSnapshot
        {
            public string Path { get; set; } = string.Empty;
            public DateTimeOffset? LastScanUtc { get; set; }
            public string? AggregatedHash { get; set; }
            public bool LastScanWasUnchanged { get; set; }
        }
    }
}
