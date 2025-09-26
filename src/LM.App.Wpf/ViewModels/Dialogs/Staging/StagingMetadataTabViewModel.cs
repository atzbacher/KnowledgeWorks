#nullable enable

using System;
using System.Collections.Generic;
using LM.App.Wpf.ViewModels;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingMetadataTabViewModel : StagingTabViewModel
    {
        private readonly StagingListViewModel _stagingList;

        public StagingMetadataTabViewModel(StagingListViewModel stagingList)
            : base("Metadata")
        {
            _stagingList = stagingList ?? throw new ArgumentNullException(nameof(stagingList));
        }

        public StagingItem? Current => _stagingList.Current;

        public Array EntryTypes => _stagingList.EntryTypes;

        public string IndexLabel => _stagingList.IndexLabel;

        public bool IsDuplicate => Current?.IsDuplicate ?? false;

        public bool IsNearMatch => Current?.IsNearMatch ?? false;

        protected override void OnItemUpdated(StagingItem? item)
        {
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(IndexLabel));
            OnPropertyChanged(nameof(IsDuplicate));
            OnPropertyChanged(nameof(IsNearMatch));
        }

        protected override void RefreshValidation()
        {
            var messages = new List<string>();
            if (Current is null)
            {
                messages.Add("Select a staged item to edit metadata.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Current.Title))
                    messages.Add("Title is required before committing.");

                if (Current.Type == EntryType.Publication && string.IsNullOrWhiteSpace(Current.AuthorsCsv))
                    messages.Add("Consider providing authors for publications.");
            }

            SetValidationMessages(messages);
        }
    }
}
