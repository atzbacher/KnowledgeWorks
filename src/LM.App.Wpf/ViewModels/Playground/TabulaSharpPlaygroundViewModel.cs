#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;

namespace LM.App.Wpf.ViewModels.Playground
{
    internal sealed partial class TabulaSharpPlaygroundViewModel : DialogViewModelBase
    {
        private readonly StagingItem _item;
        private readonly TabulaSharpPlaygroundEngine _engine;
        private TabulaSharpPlaygroundTableViewModel? _selectedTable;

        public TabulaSharpPlaygroundViewModel(StagingItem item, TabulaSharpPlaygroundEngine engine)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));

            Modes = new ObservableCollection<TabulaSharpPlaygroundMode>(TabulaSharpPlaygroundMode.CreateDefaults());
            Tables = new ObservableCollection<TabulaSharpPlaygroundTableViewModel>();
            SelectedMode = Modes.FirstOrDefault();
            Zoom = 1d;
            CurrentPage = 1;

            LoadDocumentMetadata();
            StatusMessage = SelectedMode?.Description;
        }

        public string PdfPath => _item.FilePath;

        public string PdfDisplayName => string.IsNullOrWhiteSpace(_item.FilePath)
            ? string.Empty
            : Path.GetFileName(_item.FilePath);

        public ObservableCollection<TabulaSharpPlaygroundMode> Modes { get; }

        public ObservableCollection<TabulaSharpPlaygroundTableViewModel> Tables { get; }

        public TabulaSharpPlaygroundTableViewModel? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    OnPropertyChanged(nameof(SelectedTableView));
                }
            }
        }

        public System.Data.DataView? SelectedTableView => SelectedTable?.TableView;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageCount;

        [ObservableProperty]
        private double _zoom = 1d;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private TabulaSharpPlaygroundMode? _selectedMode;

        partial void OnCurrentPageChanged(int value)
        {
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
            GoToNextPageCommand.NotifyCanExecuteChanged();
        }

        partial void OnPageCountChanged(int value)
        {
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
            GoToNextPageCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            RunCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedModeChanged(TabulaSharpPlaygroundMode? value)
        {
            if (!IsBusy)
            {
                StatusMessage = value?.Description;
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void GoToPreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage -= 1;
            }
        }

        private bool CanGoToPreviousPage() => PageCount > 0 && CurrentPage > 1;

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void GoToNextPage()
        {
            if (CurrentPage < Math.Max(1, PageCount))
            {
                CurrentPage += 1;
            }
        }

        private bool CanGoToNextPage() => PageCount > 0 && CurrentPage < Math.Max(1, PageCount);

        [RelayCommand]
        private void ZoomIn()
        {
            Zoom = Math.Clamp(Zoom + 0.15d, 0.5d, 4d);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            Zoom = Math.Clamp(Zoom - 0.15d, 0.5d, 4d);
        }

        [RelayCommand]
        private void ResetZoom()
        {
            Zoom = 1d;
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private async Task RunAsync()
        {
            if (!File.Exists(PdfPath))
            {
                StatusMessage = "The PDF could not be located on disk.";
                return;
            }

            var mode = SelectedMode ?? Modes.FirstOrDefault();
            if (mode is null)
            {
                StatusMessage = "Select an extraction profile to continue.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Running TabulaSharp…";

            try
            {
                var options = mode.CreateOptions();
                var results = await _engine.ExtractAsync(PdfPath, CurrentPage, options, CancellationToken.None).ConfigureAwait(true);
                UpdateTables(results);
            }
            catch (Exception ex)
            {
                StatusMessage = FormattableString.Invariant($"TabulaSharp failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanRun() => !IsBusy && File.Exists(PdfPath);

        [RelayCommand]
        private void Close()
        {
            RequestClose(true);
        }

        private void UpdateTables(IReadOnlyList<TabulaSharpPlaygroundTable> tables)
        {
            Tables.Clear();
            foreach (var table in tables)
            {
                Tables.Add(TabulaSharpPlaygroundTableViewModel.FromResult(table));
            }

            SelectedTable = Tables.FirstOrDefault();

            if (Tables.Count == 0)
            {
                StatusMessage = "No tables detected on the current page.";
            }
            else
            {
                StatusMessage = FormattableString.Invariant($"Found {Tables.Count} table(s) on page {CurrentPage}.");
            }
        }

        private void LoadDocumentMetadata()
        {
            if (!File.Exists(PdfPath))
            {
                StatusMessage = "The PDF could not be located on disk.";
                PageCount = 0;
                return;
            }

            try
            {
                using var document = UglyToad.PdfPig.PdfDocument.Open(PdfPath);
                PageCount = document.NumberOfPages;
                if (PageCount <= 0)
                {
                    PageCount = 1;
                }

                if (CurrentPage > PageCount)
                {
                    CurrentPage = PageCount;
                }
            }
            catch (Exception ex)
            {
                PageCount = Math.Max(1, PageCount);
                StatusMessage = FormattableString.Invariant($"Failed to read PDF: {ex.Message}");
            }
        }
    }
}
