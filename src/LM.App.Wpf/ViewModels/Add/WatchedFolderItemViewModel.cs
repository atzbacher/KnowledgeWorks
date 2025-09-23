#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LM.App.Wpf.Models;

namespace LM.App.Wpf.ViewModels.Add
{
    public sealed class WatchedFolderItemViewModel : INotifyPropertyChanged
    {
        private string _path;
        private bool _includeSubdirectories;
        private bool _isEnabled;

        public event PropertyChangedEventHandler? PropertyChanged;

        public WatchedFolderItemViewModel(string path, bool includeSubdirectories, bool isEnabled)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _includeSubdirectories = includeSubdirectories;
            _isEnabled = isEnabled;
        }

        public WatchedFolderItemViewModel(WatchedFolder folder)
            : this(folder?.Path ?? string.Empty,
                   folder?.IncludeSubdirectories ?? true,
                   folder?.IsEnabled ?? true)
        {
        }

        public string Path
        {
            get => _path;
            set
            {
                if (!string.Equals(_path, value, StringComparison.OrdinalIgnoreCase))
                {
                    _path = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeSubdirectories
        {
            get => _includeSubdirectories;
            set
            {
                if (_includeSubdirectories != value)
                {
                    _includeSubdirectories = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public WatchedFolder ToModel() => new()
        {
            Path = Path,
            IncludeSubdirectories = IncludeSubdirectories,
            IsEnabled = IsEnabled
        };

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

