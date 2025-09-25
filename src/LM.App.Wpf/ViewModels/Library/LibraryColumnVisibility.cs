using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library
{
    /// <summary>Tracks column visibility values and notifies WPF bindings.</summary>
    public sealed class LibraryColumnVisibility : INotifyPropertyChanged
    {
        private readonly Dictionary<string, bool> _states;

        public LibraryColumnVisibility()
        {
            _states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key))
                    return true;

                return _states.TryGetValue(key, out var value) ? value : true;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(key))
                    return;

                if (_states.TryGetValue(key, out var current) && current == value)
                    return;

                _states[key] = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }

        public IReadOnlyDictionary<string, bool> Snapshot() => new Dictionary<string, bool>(_states);

        public void LoadFrom(IReadOnlyDictionary<string, bool> source)
        {
            _states.Clear();
            foreach (var kvp in source)
            {
                _states[kvp.Key] = kvp.Value;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
}

