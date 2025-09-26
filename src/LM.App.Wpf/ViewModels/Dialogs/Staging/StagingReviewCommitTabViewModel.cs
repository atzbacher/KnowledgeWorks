#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingReviewCommitTabViewModel : StagingTabViewModel
    {
        private IReadOnlyList<StagingTabViewModel>? _tabs;

        public StagingReviewCommitTabViewModel()
            : base("Review & Commit")
        {
        }

        public ObservableCollection<string> Messages { get; } = new();

        public bool IsReady => Messages.Count == 0;

        public void Sync(StagingItem? item, IReadOnlyList<StagingTabViewModel> tabs)
        {
            _tabs = tabs ?? Array.Empty<StagingTabViewModel>();
            Update(item);
        }

        protected override void OnItemUpdated(StagingItem? item)
        {
            // nothing extra; validation covers summary state.
        }

        protected override void RefreshValidation()
        {
            var collected = new List<string>();

            if (Item is null)
            {
                collected.Add("Select a staged item to review before committing.");
            }
            else if (_tabs is not null)
            {
                foreach (var tab in _tabs.Where(t => !ReferenceEquals(t, this) && !t.IsValid))
                {
                    collected.AddRange(tab.ValidationErrors.Select(msg => $"{tab.Header}: {msg}"));
                }
            }

            Messages.Clear();
            foreach (var message in collected.Where(static m => !string.IsNullOrWhiteSpace(m)))
            {
                Messages.Add(message);
            }

            SetValidationMessages(collected);
            OnPropertyChanged(nameof(IsReady));
        }
    }
}
