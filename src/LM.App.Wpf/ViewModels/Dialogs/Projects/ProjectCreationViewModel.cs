using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed partial class ProjectCreationViewModel : DialogViewModelBase, IDisposable
    {
        private readonly RelayCommand _saveCommand;
        private readonly RelayCommand _cancelCommand;
        private bool _disposed;

        public ProjectCreationViewModel(ProjectCreationRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Document = new ProjectDocumentViewModel(request.Document);
            TitleAbstractStage = new StageScreeningViewModel(request.TitleAbstractStage);
            FullTextStage = new StageScreeningViewModel(request.FullTextStage);
            PdfPages = new ObservableCollection<PdfPagePreviewViewModel>(CreatePdfPages(request));
            DataExtractionGroups = new ObservableCollection<DataExtractionGroupViewModel>(CreateGroups(request));

            TitleAbstractStage.DecisionChanged += OnStageDecisionChanged;
            FullTextStage.DecisionChanged += OnStageDecisionChanged;

            _saveCommand = new RelayCommand(Save, CanSave);
            _cancelCommand = new RelayCommand(Cancel);
            UpdateDataExtractionVisibility();
        }

        public ProjectCreationRequest Request { get; }

        public string ProjectName => Request.ProjectName;

        public ProjectDocumentViewModel Document { get; }

        public StageScreeningViewModel TitleAbstractStage { get; }

        public StageScreeningViewModel FullTextStage { get; }

        public ObservableCollection<PdfPagePreviewViewModel> PdfPages { get; }

        public ObservableCollection<DataExtractionGroupViewModel> DataExtractionGroups { get; }

        [ObservableProperty]
        private bool isDataExtractionVisible;

        public RelayCommand SaveCommand => _saveCommand;

        public RelayCommand CancelCommand => _cancelCommand;

        public string WindowTitle => $"Create project – {ProjectName}";

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            TitleAbstractStage.DecisionChanged -= OnStageDecisionChanged;
            FullTextStage.DecisionChanged -= OnStageDecisionChanged;
            _disposed = true;
        }

        private bool CanSave()
        {
            return TitleAbstractStage.HasDecision && FullTextStage.HasDecision;
        }

        private void Save()
        {
            RequestClose(true);
        }

        private void Cancel()
        {
            RequestClose(false);
        }

        private void OnStageDecisionChanged(object? sender, EventArgs e)
        {
            UpdateDataExtractionVisibility();
            _saveCommand.NotifyCanExecuteChanged();
        }

        private void UpdateDataExtractionVisibility()
        {
            IsDataExtractionVisible = TitleAbstractStage.IsIncluded && FullTextStage.IsIncluded;
        }

        private static ObservableCollection<PdfPagePreviewViewModel> CreatePdfPages(ProjectCreationRequest request)
        {
            var pages = new ObservableCollection<PdfPagePreviewViewModel>();
            foreach (var definition in request.FullTextPdfPages)
            {
                pages.Add(new PdfPagePreviewViewModel(definition));
            }

            return pages;
        }

        private static ObservableCollection<DataExtractionGroupViewModel> CreateGroups(ProjectCreationRequest request)
        {
            var groups = new ObservableCollection<DataExtractionGroupViewModel>();
            foreach (var definition in request.DataExtractionGroups)
            {
                groups.Add(new DataExtractionGroupViewModel(definition));
            }

            return groups;
        }
    }
}
