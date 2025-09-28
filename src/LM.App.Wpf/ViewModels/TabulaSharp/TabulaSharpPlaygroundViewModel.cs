#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.ViewModels.TabulaSharp.Models;
using LM.App.Wpf.ViewModels.TabulaSharp.Services;

namespace LM.App.Wpf.ViewModels.TabulaSharp
{
    internal sealed class TabulaSharpPlaygroundViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly TabulaSharpPlaygroundExtractor _extractor = new();
        private readonly RelayCommand _browsePdfCommand;
        private readonly AsyncRelayCommand _extractTablesCommand;
        private readonly ObservableCollection<TabulaSharpPlaygroundTableResult> _tables = new();
        private string? _pdfPath;
        private string _statusMessage = "Select a PDF to begin.";
        private TabulaSharpPlaygroundTableResult? _selectedTable;
        private bool _isBusy;
        private CancellationTokenSource? _extractionCts;

        public TabulaSharpPlaygroundViewModel()
        {
            _browsePdfCommand = new RelayCommand(_ => BrowseForPdf(), _ => !IsBusy);
            _extractTablesCommand = new AsyncRelayCommand(ExtractAsync, () => !IsBusy && IsPdfPathValid());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TabulaSharpPlaygroundTableResult> Tables => _tables;

        public System.Windows.Input.ICommand BrowsePdfCommand => _browsePdfCommand;

        public System.Windows.Input.ICommand ExtractTablesCommand => _extractTablesCommand;

        public string? PdfPath
        {
            get => _pdfPath;
            set
            {
                if (string.Equals(_pdfPath, value, StringComparison.OrdinalIgnoreCase))
                    return;

                _pdfPath = value;
                OnPropertyChanged();
                _extractTablesCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (string.Equals(_statusMessage, value, StringComparison.Ordinal))
                    return;

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public TabulaSharpPlaygroundTableResult? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (ReferenceEquals(_selectedTable, value))
                    return;

                _selectedTable = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                _browsePdfCommand.RaiseCanExecuteChanged();
                _extractTablesCommand.RaiseCanExecuteChanged();
            }
        }

        public void Dispose()
        {
            var cts = _extractionCts;
            if (cts is null)
                return;

            _extractionCts = null;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cts.Dispose();
        }

        private void BrowseForPdf()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                PdfPath = dialog.FileName;
                StatusMessage = "Ready to extract tables.";
            }
        }

        private bool IsPdfPathValid()
            => !string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath);

        private async Task ExtractAsync()
        {
            if (!IsPdfPathValid())
            {
                StatusMessage = "Select a valid PDF file.";
                return;
            }

            var cts = new CancellationTokenSource();
            _extractionCts?.Cancel();
            _extractionCts?.Dispose();
            _extractionCts = cts;

            IsBusy = true;
            try
            {
                StatusMessage = "Running TabulaSharp heuristics…";
                var results = await _extractor.ExtractAsync(_pdfPath!, cts.Token);

                _tables.Clear();
                foreach (var result in results)
                {
                    _tables.Add(result);
                }

                SelectedTable = _tables.FirstOrDefault();
                StatusMessage = _tables.Count == 0
                    ? "No tables detected."
                    : FormattableString.Invariant($"Detected {_tables.Count} table(s).");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Extraction canceled.";
            }
            catch (Exception ex)
            {
                StatusMessage = FormattableString.Invariant($"Extraction failed: {ex.Message}");
            }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_extractionCts, cts))
                {
                    _extractionCts = null;
                }

                IsBusy = false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
