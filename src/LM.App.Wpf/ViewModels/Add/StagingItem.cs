#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LM.Core.Models;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels
{
    public sealed class StagingItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public const double DuplicateThreshold = 0.999;
        public const double NearThreshold = 0.75;
        public string? AttachToEntryId { get; set; }      // user can type/paste a target entry Id
        public string? AttachToTitle { get; set; }

        public bool Selected { get; set; }
        public string FilePath { get; set; } = "";
        public string OriginalFileName => System.IO.Path.GetFileName(FilePath);

        private EntryType _type = EntryType.Publication;
        public EntryType Type
        {
            get => _type;
            set { if (_type != value) { _type = value; Raise(); } }
        }

        private string? _title;
        public string? Title
        {
            get => _title;
            set { if (!Equals(_title, value)) { _title = value; Raise(); } }
        }

        private string? _displayName;
        public string? DisplayName
        {
            get => _displayName;
            set { if (!Equals(_displayName, value)) { _displayName = value; Raise(); } }
        }

        public string? AuthorsCsv { get; set; }
        public int? Year { get; set; }
        public string? Source { get; set; }
        public string? Doi { get; set; }
        public string? Pmid { get; set; }
        public string? TagsCsv { get; set; }
        public bool IsInternal { get; set; }

        public string? InternalId { get; set; }
        public string? Notes { get; set; }

        private double _similarity;
        public double Similarity
        {
            get => _similarity;
            set
            {
                if (Math.Abs(_similarity - value) > 0.000001)
                {
                    _similarity = value;
                    Raise();
                    Raise(nameof(IsDuplicate));
                    Raise(nameof(IsNearMatch));
                }
            }
        }

        public string? SimilarToEntryId { get; set; }
        public string? SimilarToTitle { get; set; }
        public string? MatchedTitle { get => SimilarToTitle; set => SimilarToTitle = value; }

        public bool IsDuplicate => _similarity >= DuplicateThreshold;
        public bool IsNearMatch => _similarity >= NearThreshold && _similarity < DuplicateThreshold;

        private string _suggestedAction = "New";
        public string SuggestedAction
        {
            get => _suggestedAction;
            set { if (!string.Equals(_suggestedAction, value, StringComparison.Ordinal)) { _suggestedAction = value; Raise(); } }
        }

        public bool Internal { get => IsInternal; set => IsInternal = value; }

        // NEW: the fully-populated hooks built at staging
        public HookM.ArticleHook? ArticleHook { get; set; }
        public HookM.DataExtractionHook? DataExtractionHook { get; set; }
        public List<HookM.EntryChangeLogEvent> PendingChangeLogEvents { get; } = new();
        public StagingEvidencePreview? EvidencePreview { get; set; }
    }
}
