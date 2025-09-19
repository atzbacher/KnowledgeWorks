#nullable enable
using System;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value ?? string.Empty);
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

        public void MarkScanned(DateTimeOffset whenUtc)
        {
            LastScanUtc = whenUtc;
        }

        internal WatchedFolder Clone() => new()
        {
            Path = Path,
            IsEnabled = IsEnabled
        };

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

        public static async Task<WatchedFolderConfig> LoadAsync(string path, CancellationToken ct)
        {
            var config = new WatchedFolderConfig();
            if (!File.Exists(path))
                return config;

            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var snapshot = await JsonSerializer.DeserializeAsync<WatchedFolderConfigSnapshot>(stream, JsonOptions, ct).ConfigureAwait(false);
                if (snapshot?.Folders is not null)
                {
                    foreach (var folder in snapshot.Folders)
                    {
                        if (string.IsNullOrWhiteSpace(folder.Path))
                            continue;
                        config.Folders.Add(new WatchedFolder
                        {
                            Path = folder.Path,
                            IsEnabled = folder.IsEnabled
                        });
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
            var snapshot = new WatchedFolderConfigSnapshot
            {
                Folders = Folders.Select(static f => new WatchedFolderSnapshot
                {
                    Path = f.Path,
                    IsEnabled = f.IsEnabled
                }).ToArray()
            };

            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, ct).ConfigureAwait(false);
        }

        private sealed class WatchedFolderConfigSnapshot
        {
            public WatchedFolderSnapshot[]? Folders { get; set; }
        }

        private sealed class WatchedFolderSnapshot
        {
            public string Path { get; set; } = string.Empty;
            public bool IsEnabled { get; set; } = true;
        }
    }
}
