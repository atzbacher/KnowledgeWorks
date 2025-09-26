#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.Services.Review;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed class LitSearchRunEntryItemViewModel : ObservableObject
    {
        internal LitSearchRunEntryItemViewModel(LitSearchRunOption option)
        {
            Option = option ?? throw new ArgumentNullException(nameof(option));
            Runs = new ObservableCollection<LitSearchRunItemViewModel>(
                option.Runs.Select(run => new LitSearchRunItemViewModel(this, run)));
        }

        public string EntryId => Option.EntryId;

        public string Label => Option.Label;

        public string Query => Option.Query;

        public string HookAbsolutePath => Option.HookAbsolutePath;

        public string HookRelativePath => Option.HookRelativePath;

        public ObservableCollection<LitSearchRunItemViewModel> Runs { get; }

        internal LitSearchRunOption Option { get; }
    }

    public sealed class LitSearchRunItemViewModel : ObservableObject
    {
        internal LitSearchRunItemViewModel(LitSearchRunEntryItemViewModel owner, LitSearchRunOptionRun run)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public LitSearchRunEntryItemViewModel Owner { get; }

        internal LitSearchRunOptionRun Run { get; }

        public string RunId => Run.RunId;

        public DateTime RunUtc => Run.RunUtc;

        public int TotalHits => Run.TotalHits;

        public string? ExecutedBy => Run.ExecutedBy;

        public bool IsFavorite => Run.IsFavorite;

        public string DisplayLabel
        {
            get
            {
                var timestamp = Run.RunUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
                var prefix = Run.IsFavorite ? "★" : "";
                var hits = Run.TotalHits.ToString(CultureInfo.CurrentCulture);
                if (string.IsNullOrWhiteSpace(Run.ExecutedBy))
                {
                    return string.IsNullOrEmpty(prefix)
                        ? $"{timestamp} · {hits} hits"
                        : $"{prefix} {timestamp} · {hits} hits";
                }

                var executedBy = Run.ExecutedBy.Trim();
                return string.IsNullOrEmpty(prefix)
                    ? $"{timestamp} · {hits} hits · {executedBy}"
                    : $"{prefix} {timestamp} · {hits} hits · {executedBy}";
            }
        }
    }
}
