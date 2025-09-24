using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using LM.Core.Models;

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

        public string LastScanDisplay => _lastScanUtc is null
            ? "Never"
            : _lastScanUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

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

        public string ScanStatusLabel => _lastScanWasUnchanged switch
        {
            true => "Unchanged",
            false => "Changed",
            _ => "Not scanned"
        };

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
}
