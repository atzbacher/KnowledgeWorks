using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.Core.Abstractions.Search;
using LM.Core.Models;
using LM.Core.Models.Search;

namespace LM.App.Wpf.ViewModels.Search
{
    /// <summary>
    /// Handles executing searches against external providers and maintaining the in-memory results.
    /// </summary>
    public sealed class SearchProvidersViewModel : ViewModelBase
    {
        private readonly ISearchExecutionService _executionService;
        private bool _isRunning;
        private bool _isLocked;

        public SearchProvidersViewModel(ISearchExecutionService executionService)
        {
            _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));

            Results = new ObservableCollection<SearchHit>();
            RunSearchCommand = new AsyncRelayCommand(RunSearchInternalAsync, CanRunSearch);
        }

        public ObservableCollection<SearchHit> Results { get; }

        private string _query = string.Empty;
        public string Query
        {
            get => _query;
            set
            {
                if (_query == value)
                    return;
                _query = value;
                OnPropertyChanged();
                RaiseCommandState();
            }
        }

        public IReadOnlyList<SearchDatabaseOption> Databases { get; } = new[]
        {
            new SearchDatabaseOption(SearchDatabase.PubMed, "PubMed"),
            new SearchDatabaseOption(SearchDatabase.ClinicalTrialsGov, "ClinicalTrials.gov")
        };

        private SearchDatabase _selectedDatabase = SearchDatabase.PubMed;
        public SearchDatabase SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (_selectedDatabase == value)
                    return;
                _selectedDatabase = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _from;
        public DateTime? From
        {
            get => _from;
            set
            {
                if (_from == value)
                    return;
                _from = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _to;
        public DateTime? To
        {
            get => _to;
            set
            {
                if (_to == value)
                    return;
                _to = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy => _isRunning;

        public AsyncRelayCommand RunSearchCommand { get; }

        public event EventHandler<SearchExecutedEventArgs>? SearchExecuted;

        internal void SetLocked(bool value)
        {
            if (_isLocked == value)
                return;
            _isLocked = value;
            RaiseCommandState();
        }

        private bool CanRunSearch()
            => !_isRunning && !_isLocked && !string.IsNullOrWhiteSpace(Query);

        public Task ExecuteSearchAsync() => RunSearchInternalAsync();

        private async Task RunSearchInternalAsync()
        {
            Results.Clear();
            _isRunning = true;
            OnPropertyChanged(nameof(IsBusy));
            RaiseCommandState();

            var request = new SearchExecutionRequest
            {
                Query = Query,
                Database = SelectedDatabase,
                From = From,
                To = To
            };

            try
            {
                var result = await _executionService.ExecuteAsync(request, CancellationToken.None);

                foreach (var hit in result.Hits)
                    Results.Add(hit);

                SearchExecuted?.Invoke(this, new SearchExecutedEventArgs(result));
            }
            catch (Exception ex)
            {
                HandleSearchError(request.Database, ex);
            }
            finally
            {
                _isRunning = false;
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommandState();
            }
        }

        private void RaiseCommandState()
            => RunSearchCommand.RaiseCanExecuteChanged();

        private void HandleSearchError(SearchDatabase database, Exception exception)
        {
            var providerName = GetProviderDisplayName(database);
            string message;

            if (exception is HttpRequestException httpException)
            {
                var statusSuffix = httpException.StatusCode is { } statusCode
                    ? $" (HTTP {(int)statusCode} - {statusCode})"
                    : string.Empty;

                var detail = httpException.InnerException?.Message;
                if (string.IsNullOrWhiteSpace(detail))
                    detail = httpException.Message;

                message = $"Could not reach {providerName}{statusSuffix}.{Environment.NewLine}{detail}";
            }
            else
            {
                message = $"Search against {providerName} failed.{Environment.NewLine}{exception.Message}";
            }

            Trace.WriteLine($"[SearchProvidersViewModel] {database} search failed: {exception}");

            System.Windows.MessageBox.Show(
                message,
                "Search Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }

        private string GetProviderDisplayName(SearchDatabase database)
        {
            foreach (var option in Databases)
            {
                if (option.Value == database)
                    return option.DisplayName;
            }

            return database.ToString();
        }
    }

    public sealed class SearchExecutedEventArgs : EventArgs
    {
        public SearchExecutedEventArgs(SearchExecutionResult result)
            => Result = result;

        public SearchExecutionResult Result { get; }
    }
}
