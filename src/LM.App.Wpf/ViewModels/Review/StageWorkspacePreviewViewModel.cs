#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review;

internal sealed class StageWorkspacePreviewViewModel : ObservableObject, IDisposable
{
    private readonly StageBlueprintViewModel _stage;
    private readonly List<StageDisplayOptionViewModel> _trackedOptions = new();
    private bool _hasBibliographySummary;
    private bool _hasInclusionChecklist;
    private bool _hasFullTextViewer;
    private bool _hasDecisionPanel;
    private bool _hasDataExtractionWorkspace;
    private bool _hasNotesPane;
    private System.Windows.GridLength _leftColumnWidth = new(3, System.Windows.GridUnitType.Star);
    private System.Windows.GridLength _decisionColumnWidth = new(2, System.Windows.GridUnitType.Star);
    private System.Windows.GridLength _extractionColumnWidth = new(0);

    private static readonly IReadOnlyList<string> InclusionChecklistItems = new[]
    {
        "Population aligns with scope",
        "Intervention matches criteria",
        "Comparator is relevant",
        "Outcomes address key measures",
        "Study design fits protocol"
    };

    private static readonly IReadOnlyList<string> ExtractionFieldCaptions = new[]
    {
        "Population details",
        "Intervention notes",
        "Comparator summary",
        "Primary outcomes",
        "Key quotes & annotations"
    };

    public StageWorkspacePreviewViewModel(StageBlueprintViewModel stage)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));
        _stage.PropertyChanged += OnStagePropertyChanged;

        if (_stage.DisplayOptions is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnDisplayOptionsChanged;
        }

        TrackDisplayOptions();
        RefreshDerivedState();
    }

    public string StageName => _stage.Name;

    public ReviewStageType StageType => _stage.StageType;

    public bool IsTitleScreening => StageType == ReviewStageType.TitleScreening;

    public bool IsFullTextReview => StageType == ReviewStageType.FullTextReview;

    public bool IsDataExtractionStage => StageType == ReviewStageType.DataExtraction;

    public bool HasBibliographySummary
    {
        get => _hasBibliographySummary;
        private set => SetProperty(ref _hasBibliographySummary, value);
    }

    public bool HasInclusionChecklist
    {
        get => _hasInclusionChecklist;
        private set => SetProperty(ref _hasInclusionChecklist, value);
    }

    public bool HasFullTextViewer
    {
        get => _hasFullTextViewer;
        private set => SetProperty(ref _hasFullTextViewer, value);
    }

    public bool HasDecisionPanel
    {
        get => _hasDecisionPanel;
        private set => SetProperty(ref _hasDecisionPanel, value);
    }

    public bool HasDataExtractionWorkspace
    {
        get => _hasDataExtractionWorkspace;
        private set => SetProperty(ref _hasDataExtractionWorkspace, value);
    }

    public bool HasNotesPane
    {
        get => _hasNotesPane;
        private set => SetProperty(ref _hasNotesPane, value);
    }

    public bool HasLeftPane => HasBibliographySummary || HasFullTextViewer;

    public bool HasDecisionPane => HasDecisionPanel || HasInclusionChecklist || HasNotesPane;

    public bool HasExtractionPane => HasDataExtractionWorkspace;

    public System.Windows.GridLength LeftColumnWidth
    {
        get => _leftColumnWidth;
        private set => SetProperty(ref _leftColumnWidth, value);
    }

    public System.Windows.GridLength DecisionColumnWidth
    {
        get => _decisionColumnWidth;
        private set => SetProperty(ref _decisionColumnWidth, value);
    }

    public System.Windows.GridLength ExtractionColumnWidth
    {
        get => _extractionColumnWidth;
        private set => SetProperty(ref _extractionColumnWidth, value);
    }

    public IReadOnlyList<string> InclusionChecklist => InclusionChecklistItems;

    public IReadOnlyList<string> ExtractionFields => ExtractionFieldCaptions;

    public string StageSubtitle => StageType switch
    {
        ReviewStageType.TitleScreening => "Quickly triage titles and abstracts with structured context.",
        ReviewStageType.FullTextReview => "Assess full manuscripts while keeping inclusion decisions in view.",
        ReviewStageType.DataExtraction => "Capture structured evidence from previously included studies.",
        _ => "Configure reviewer workspace panels for this stage."
    };

    public string PrimaryDecisionLabel => StageType switch
    {
        ReviewStageType.TitleScreening => "Include (meets PICO(S))",
        ReviewStageType.FullTextReview => "Include full text",
        ReviewStageType.DataExtraction => "Ready for extraction",
        _ => "Include"
    };

    public string SecondaryDecisionLabel => StageType switch
    {
        ReviewStageType.TitleScreening => "Exclude (out of scope)",
        ReviewStageType.FullTextReview => "Exclude (insufficient detail)",
        ReviewStageType.DataExtraction => "Flag for follow-up",
        _ => "Exclude"
    };

    public string DecisionHint => StageType switch
    {
        ReviewStageType.TitleScreening => "Use shortcuts: ← to exclude, → to include.",
        ReviewStageType.FullTextReview => "Track rationale for exclusion to feed PRISMA diagrams.",
        ReviewStageType.DataExtraction => "Capture consensus, then hand off to QA reviewers.",
        _ => "Capture reviewer choices and rationale."
    };

    private void OnStagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StageBlueprintViewModel.StageType))
        {
            OnPropertyChanged(nameof(StageType));
            OnPropertyChanged(nameof(IsTitleScreening));
            OnPropertyChanged(nameof(IsFullTextReview));
            OnPropertyChanged(nameof(IsDataExtractionStage));
            RefreshDerivedState();
        }
        else if (e.PropertyName is nameof(StageBlueprintViewModel.Name))
        {
            OnPropertyChanged(nameof(StageName));
        }
    }

    private void OnDisplayOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TrackDisplayOptions();
        RefreshDerivedState();
    }

    private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StageDisplayOptionViewModel.IsSelected))
        {
            RefreshDerivedState();
        }
    }

    private void TrackDisplayOptions()
    {
        foreach (var option in _trackedOptions)
        {
            option.PropertyChanged -= OnOptionPropertyChanged;
        }

        _trackedOptions.Clear();

        foreach (var option in _stage.DisplayOptions)
        {
            option.PropertyChanged += OnOptionPropertyChanged;
            _trackedOptions.Add(option);
        }
    }

    private void RefreshDerivedState()
    {
        HasBibliographySummary = IsAreaEnabled(StageContentArea.BibliographySummary);
        HasInclusionChecklist = IsAreaEnabled(StageContentArea.InclusionExclusionChecklist);
        HasFullTextViewer = IsAreaEnabled(StageContentArea.FullTextViewer);
        HasDecisionPanel = IsAreaEnabled(StageContentArea.ReviewerDecisionPanel);
        HasDataExtractionWorkspace = IsAreaEnabled(StageContentArea.DataExtractionWorkspace);
        HasNotesPane = IsAreaEnabled(StageContentArea.NotesPane);

        LeftColumnWidth = HasLeftPane
            ? new System.Windows.GridLength(3, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(0);

        DecisionColumnWidth = HasDecisionPane
            ? new System.Windows.GridLength(2, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(0);

        ExtractionColumnWidth = HasExtractionPane
            ? new System.Windows.GridLength(2, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(0);

        OnPropertyChanged(nameof(HasLeftPane));
        OnPropertyChanged(nameof(HasDecisionPane));
        OnPropertyChanged(nameof(HasExtractionPane));
        OnPropertyChanged(nameof(StageSubtitle));
        OnPropertyChanged(nameof(PrimaryDecisionLabel));
        OnPropertyChanged(nameof(SecondaryDecisionLabel));
        OnPropertyChanged(nameof(DecisionHint));
    }

    private bool IsAreaEnabled(StageContentArea area)
    {
        return _stage.DisplayOptions.Any(option => option.Area == area && option.IsSelected);
    }

    public void Dispose()
    {
        _stage.PropertyChanged -= OnStagePropertyChanged;

        if (_stage.DisplayOptions is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnDisplayOptionsChanged;
        }

        foreach (var option in _trackedOptions)
        {
            option.PropertyChanged -= OnOptionPropertyChanged;
        }

        _trackedOptions.Clear();
    }
}
