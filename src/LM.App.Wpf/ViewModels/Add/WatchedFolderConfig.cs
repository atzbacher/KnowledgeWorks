using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>Collection wrapper that tracks watched folders and their scan state.</summary>
    public sealed class WatchedFolderConfig
    {
        public ObservableCollection<WatchedFolder> Folders { get; } = new();

        private readonly Dictionary<string, WatchedFolderState> _states = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<WatchedFolder, string> _stateKeys = new();
        private readonly object _stateGate = new();

        public void Load(WatchedFolderSettings settings)
        {
            if (settings is null)
                throw new ArgumentNullException(nameof(settings));

            Folders.Clear();

            lock (_stateGate)
            {
                _states.Clear();
                _stateKeys.Clear();
            }

            foreach (var state in settings.States ?? Array.Empty<WatchedFolderState>())
            {
                if (string.IsNullOrWhiteSpace(state.Path))
                    continue;
                StoreStateInternal(state);
            }

            foreach (var folder in settings.Folders ?? Array.Empty<WatchedFolderSettingsFolder>())
            {
                if (string.IsNullOrWhiteSpace(folder.Path))
                    continue;

                var watched = new WatchedFolder
                {
                    Path = folder.Path,
                    IsEnabled = folder.IsEnabled
                };

                Folders.Add(watched);
                ApplyState(watched);
            }
        }

        public WatchedFolderSettings CreateSnapshot()
        {
            WatchedFolderSettingsFolder[] folders;
            var states = new List<WatchedFolderState>();

            lock (_stateGate)
            {
                folders = Folders
                    .Select(static f => new WatchedFolderSettingsFolder
                    {
                        Path = f.Path,
                        IsEnabled = f.IsEnabled
                    })
                    .ToArray();

                foreach (var folder in Folders)
                {
                    if (!_stateKeys.TryGetValue(folder, out var key))
                    {
                        key = NormalizePath(folder.Path);
                    }

                    if (_states.TryGetValue(key, out var state))
                    {
                        states.Add(state with { Path = folder.Path });
                    }
                }
            }

            return new WatchedFolderSettings
            {
                Folders = folders.Length == 0 ? Array.Empty<WatchedFolderSettingsFolder>() : folders,
                States = states.Count == 0 ? Array.Empty<WatchedFolderState>() : states.ToArray()
            };
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
    }
}
