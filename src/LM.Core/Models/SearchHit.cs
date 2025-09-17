#nullable enable
using System;

namespace LM.Core.Models
{
    /// <summary>Provider-agnostic search row used in the Search tab UI.</summary>
    public sealed class SearchHit : System.ComponentModel.INotifyPropertyChanged
    {
        public SearchDatabase Source { get; init; }
        public string ExternalId { get; init; } = "";   // PMID or NCT
        public string? Doi { get; init; }
        public string Title { get; init; } = "";
        public string Authors { get; init; } = "";
        public string? JournalOrSource { get; init; }
        public int? Year { get; init; }
        public string? Url { get; init; }

        private bool _alreadyInDb;    // computed in VM
        public bool AlreadyInDb
        {
            get => _alreadyInDb;
            set
            {
                if (_alreadyInDb == value)
                    return;
                _alreadyInDb = value;
                OnPropertyChanged(nameof(AlreadyInDb));
            }
        }

        private bool _selected = true;
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;
                _selected = value;
                OnPropertyChanged(nameof(Selected));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
