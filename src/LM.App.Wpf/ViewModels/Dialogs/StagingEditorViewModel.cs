using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.Infrastructure.Utils;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    internal sealed partial class StagingEditorViewModel : DialogViewModelBase, IDisposable
    {
        private readonly StagingReviewCommitTabViewModel _reviewTab;
        private StagingTabViewModel? _selectedTab;

        public StagingEditorViewModel(StagingListViewModel stagingList,
                                      StagingMetadataTabViewModel metadataTab,
                                      StagingTablesTabViewModel tablesTab,
                                      StagingFiguresTabViewModel figuresTab,
                                      StagingEndpointsTabViewModel endpointsTab,
                                      StagingPopulationTabViewModel populationTab,
                                      StagingReviewCommitTabViewModel reviewTab)
        {
            StagingList = stagingList ?? throw new ArgumentNullException(nameof(stagingList));
            StagingList.PropertyChanged += OnStagingListPropertyChanged;

            if (metadataTab is null) throw new ArgumentNullException(nameof(metadataTab));
            if (tablesTab is null) throw new ArgumentNullException(nameof(tablesTab));
            if (figuresTab is null) throw new ArgumentNullException(nameof(figuresTab));
            if (endpointsTab is null) throw new ArgumentNullException(nameof(endpointsTab));
            if (populationTab is null) throw new ArgumentNullException(nameof(populationTab));
            _reviewTab = reviewTab ?? throw new ArgumentNullException(nameof(reviewTab));

            Tabs = new ObservableCollection<StagingTabViewModel>
            {
                metadataTab,
                tablesTab,
                figuresTab,
                endpointsTab,
                populationTab,
                _reviewTab
            };

            SelectedTab = Tabs.FirstOrDefault();
            UpdateActiveItem();
        }

        public StagingListViewModel StagingList { get; }

        public ObservableCollection<StagingTabViewModel> Tabs { get; }

        public StagingTabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                    UpdateActiveTabState();
            }
        }

        public bool HasValidationErrors => Tabs.Any(tab => !tab.IsValid);

        [RelayCommand(CanExecute = nameof(CanNavigate))]
        private void Prev()
        {
            StagingList.SelectByOffset(-1);
        }

        [RelayCommand(CanExecute = nameof(CanNavigate))]
        private void Next()
        {
            StagingList.SelectByOffset(+1);
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose(false);
        }

        [RelayCommand(CanExecute = nameof(CanGenerateShortTitle))]
        private void GenerateShortTitle()
        {
            var current = StagingList.Current;
            if (current is null)
                return;

            var authors = (current.AuthorsCsv ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(author => author.Trim())
                .Where(static author => !string.IsNullOrEmpty(author))
                .ToList();

            current.DisplayName = BibliographyHelper.GenerateShortTitle(
                current.Title,
                authors,
                current.Source,
                current.Year);
        }

        private bool CanNavigate() => StagingList.HasItems;

        private bool CanGenerateShortTitle() => StagingList.Current is not null;

        public void Dispose()
        {
            StagingList.PropertyChanged -= OnStagingListPropertyChanged;
        }

        private void OnStagingListPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StagingListViewModel.HasItems))
            {
                PrevCommand.NotifyCanExecuteChanged();
                NextCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(StagingListViewModel.Current))
            {
                GenerateShortTitleCommand.NotifyCanExecuteChanged();
                UpdateActiveItem();
            }
            else if (e.PropertyName == nameof(StagingListViewModel.IndexLabel))
            {
                UpdateActiveItem();
            }
        }

        private void UpdateActiveItem()
        {
            var current = StagingList.Current;
            foreach (var tab in Tabs)
            {
                tab.Update(current);
            }

            _reviewTab.Sync(current, Tabs.ToList());
            UpdateActiveTabState();
            OnPropertyChanged(nameof(HasValidationErrors));
        }

        private void UpdateActiveTabState()
        {
            foreach (var tab in Tabs)
            {
                tab.IsActive = ReferenceEquals(tab, SelectedTab);
            }

            OnPropertyChanged(nameof(HasValidationErrors));
        }
    }
}
