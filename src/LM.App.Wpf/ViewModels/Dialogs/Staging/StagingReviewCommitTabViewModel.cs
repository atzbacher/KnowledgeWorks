#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;


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


        public IAsyncRelayCommand CommitExtractionCommand => _commitExtractionCommand;

        public IAsyncRelayCommand CommitMetadataOnlyCommand => _commitMetadataOnlyCommand;

        public void Sync(StagingItem? item, IReadOnlyList<StagingTabViewModel> tabs)
        {
            _tabs = tabs ?? Array.Empty<StagingTabViewModel>();
            _tablesTab = _tabs.OfType<StagingTablesTabViewModel>().FirstOrDefault();
            _endpointsTab = _tabs.OfType<StagingEndpointsTabViewModel>().FirstOrDefault();
            Update(item);
        }

        protected override void OnItemUpdated(StagingItem? item)
        {
            // nothing extra; validation covers summary state.
            UpdateCommandStates();

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

            UpdateCommandStates();
        }

        private bool CanCommit()
            => Item is not null && IsReady;

        private async Task CommitExtractionAsync()
        {
            if (Item is null)
                return;

            var current = Item;
            var existingHook = current.DataExtractionHook;
            try
            {
                var tables = _tablesTab?.Tables ?? Array.Empty<StagingTableRowViewModel>();
                var endpoints = _endpointsTab?.Endpoints ?? Array.Empty<StagingEndpointViewModel>();
                var built = _builder.Build(current, tables, endpoints);
                if (built is not null)
                    current.DataExtractionHook = built;

                current.CommitMetadataOnly = false;
                await _stagingList.CommitAsync(new[] { current }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                current.CommitMetadataOnly = false;
                if (_stagingList.Items.Contains(current))
                {
                    current.DataExtractionHook = existingHook;
                }
            }
        }

        private async Task CommitMetadataOnlyAsync()
        {
            if (Item is null)
                return;

            var current = Item;
            var existingHook = current.DataExtractionHook;
            try
            {
                current.CommitMetadataOnly = true;
                await _stagingList.CommitAsync(new[] { current }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                current.CommitMetadataOnly = false;
                if (_stagingList.Items.Contains(current))
                {
                    current.DataExtractionHook = existingHook;
                }
            }
        }

        private void UpdateCommandStates()
        {
            _commitExtractionCommand.NotifyCanExecuteChanged();
            _commitMetadataOnlyCommand.NotifyCanExecuteChanged();

        }
    }
}
