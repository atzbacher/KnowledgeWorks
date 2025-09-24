using System;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.Infrastructure.Utils;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed partial class StagingEditorViewModel : DialogViewModelBase, IDisposable
    {
        public StagingEditorViewModel(StagingListViewModel stagingList)
        {
            StagingList = stagingList ?? throw new ArgumentNullException(nameof(stagingList));
            StagingList.PropertyChanged += OnStagingListPropertyChanged;
        }

        public StagingListViewModel StagingList { get; }

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
            }
        }
    }
}
