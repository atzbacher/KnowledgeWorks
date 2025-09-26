#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.ViewModels;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal abstract class StagingTabViewModel : ObservableObject
    {
        private bool _isActive;
        private StagingItem? _currentItem;

        protected StagingTabViewModel(string header)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
        }

        public string Header { get; }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        protected StagingItem? Item => _currentItem;

        public ObservableCollection<string> ValidationErrors { get; } = new();

        public virtual bool IsValid => ValidationErrors.Count == 0;

        public void Update(StagingItem? item)
        {
            _currentItem = item;
            OnItemUpdated(item);
            RefreshValidation();
        }

        protected abstract void OnItemUpdated(StagingItem? item);

        protected virtual void RefreshValidation()
        {
            ValidationErrors.Clear();
            OnPropertyChanged(nameof(IsValid));
        }

        protected void SetValidationMessages(IEnumerable<string> messages)
        {
            ValidationErrors.Clear();
            foreach (var message in messages.Where(static m => !string.IsNullOrWhiteSpace(m)))
            {
                ValidationErrors.Add(message);
            }

            OnPropertyChanged(nameof(IsValid));
        }
    }
}
