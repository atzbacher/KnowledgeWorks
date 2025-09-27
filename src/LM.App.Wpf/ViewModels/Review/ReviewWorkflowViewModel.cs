namespace LM.App.Wpf.ViewModels.Review;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Review.Core.Models;

internal sealed class ReviewWorkflowViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<string> _litSearchRuns;
    private readonly ObservableCollection<ReviewLayerViewModel> _layers;
    private readonly ObservableCollection<ReviewStepViewModel> _steps;
    private readonly ReadOnlyObservableCollection<string> _readOnlyRuns;
    private readonly ReadOnlyObservableCollection<ReviewStepViewModel> _readOnlySteps;
    private readonly IReviewAuditService _auditService;

    private readonly RelayCommand _nextCommand;
    private readonly RelayCommand _backCommand;
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _addLayerCommand;
    private readonly RelayCommand _removeLayerCommand;

    private int _currentStepIndex;
    private string? _selectedLitSearchRun;
    private ReviewProjectType _selectedProjectType = ReviewProjectType.Picos;
    private string _reviewTitle;
    private string? _metadataNotes;
    private ReviewLayerViewModel? _selectedLayer;
    private readonly string _createdBy;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ReviewWorkflowCompletedEventArgs>? Completed;
    public event EventHandler? Canceled;

    public ReviewWorkflowViewModel(IEnumerable<string> litSearchRuns, IReviewAuditService auditService, string? suggestedTitle)
    {
        if (auditService is null)
        {
            throw new ArgumentNullException(nameof(auditService));
        }

        _auditService = auditService;
        _litSearchRuns = new ObservableCollection<string>(litSearchRuns?.Distinct(StringComparer.OrdinalIgnoreCase) ?? Array.Empty<string>());
        _layers = new ObservableCollection<ReviewLayerViewModel>();
        _steps = new ObservableCollection<ReviewStepViewModel>
        {
            new ReviewStepViewModel("Select run", ReviewWorkflowStep.SelectRun),
            new ReviewStepViewModel("Configure layers", ReviewWorkflowStep.ConfigureLayers),
            new ReviewStepViewModel("Overview", ReviewWorkflowStep.Overview)
        };

        _readOnlyRuns = new ReadOnlyObservableCollection<string>(_litSearchRuns);
        _readOnlySteps = new ReadOnlyObservableCollection<ReviewStepViewModel>(_steps);

        _createdBy = Environment.UserName;
        _reviewTitle = suggestedTitle ?? string.Empty;

        _nextCommand = new RelayCommand(_ => MoveNext(), _ => CanMoveNext());
        _backCommand = new RelayCommand(_ => MovePrevious(), _ => CanMovePrevious());
        _saveCommand = new RelayCommand(_ => Save(), _ => CanSave());
        _cancelCommand = new RelayCommand(_ => Cancel());
        _addLayerCommand = new RelayCommand(_ => AddLayer());
        _removeLayerCommand = new RelayCommand(_ => RemoveLayer(), _ => SelectedLayer is not null);

        if (_litSearchRuns.Count > 0)
        {
            _selectedLitSearchRun = _litSearchRuns[0];
        }

        SeedDefaultLayers();
        UpdateStepActivation();
    }

    public ReadOnlyObservableCollection<string> LitSearchRuns => _readOnlyRuns;

    public bool HasLitSearchRuns => _litSearchRuns.Count > 0;

    public IReadOnlyList<ReviewProjectType> ProjectTypes { get; } = Enum.GetValues<ReviewProjectType>();

    public IReadOnlyList<ReviewLayerKind> LayerKinds { get; } = Enum.GetValues<ReviewLayerKind>();

    public IReadOnlyList<ReviewLayerDisplayMode> DisplayModes { get; } = Enum.GetValues<ReviewLayerDisplayMode>();

    public ObservableCollection<ReviewLayerViewModel> Layers => _layers;

    public ReadOnlyObservableCollection<ReviewStepViewModel> Steps => _readOnlySteps;

    public ReviewWorkflowStep CurrentStep => _steps[_currentStepIndex].Step;

    public bool IsOnLastStep => _currentStepIndex == _steps.Count - 1;

    public bool IsOnFirstStep => _currentStepIndex == 0;

    public string? SelectedLitSearchRun
    {
        get => _selectedLitSearchRun;
        set
        {
            if (!string.Equals(_selectedLitSearchRun, value, StringComparison.Ordinal))
            {
                _selectedLitSearchRun = value;
                OnPropertyChanged();
            }
        }
    }

    public ReviewProjectType SelectedProjectType
    {
        get => _selectedProjectType;
        set
        {
            if (_selectedProjectType != value)
            {
                _selectedProjectType = value;
                OnPropertyChanged();
            }
        }
    }

    public string ReviewTitle
    {
        get => _reviewTitle;
        set
        {
            if (!string.Equals(_reviewTitle, value, StringComparison.Ordinal))
            {
                _reviewTitle = value ?? string.Empty;
                OnPropertyChanged();
                RaiseGuards();
            }
        }
    }

    public string? MetadataNotes
    {
        get => _metadataNotes;
        set
        {
            if (!string.Equals(_metadataNotes, value, StringComparison.Ordinal))
            {
                _metadataNotes = value;
                OnPropertyChanged();
            }
        }
    }

    public ReviewLayerViewModel? SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (!ReferenceEquals(_selectedLayer, value))
            {
                _selectedLayer = value;
                OnPropertyChanged();
                _removeLayerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CreatedBy => _createdBy;

    public IEnumerable<ReviewLayerViewModel> OverviewLayers => _layers;

    public System.Windows.Input.ICommand NextCommand => _nextCommand;
    public System.Windows.Input.ICommand BackCommand => _backCommand;
    public System.Windows.Input.ICommand SaveCommand => _saveCommand;
    public System.Windows.Input.ICommand CancelCommand => _cancelCommand;
    public System.Windows.Input.ICommand AddLayerCommand => _addLayerCommand;
    public System.Windows.Input.ICommand RemoveLayerCommand => _removeLayerCommand;

    private void MoveNext()
    {
        if (!CanMoveNext())
        {
            return;
        }

        _currentStepIndex++;
        UpdateStepActivation();
        RaiseGuards();
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsOnLastStep));
        OnPropertyChanged(nameof(IsOnFirstStep));
    }

    private void MovePrevious()
    {
        if (!CanMovePrevious())
        {
            return;
        }

        _currentStepIndex--;
        UpdateStepActivation();
        RaiseGuards();
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsOnLastStep));
        OnPropertyChanged(nameof(IsOnFirstStep));
    }

    private void Cancel()
    {
        Canceled?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        if (!CanSave())
        {
            return;
        }

        var definition = BuildDefinition();
        _auditService.Append(definition);
        Completed?.Invoke(this, new ReviewWorkflowCompletedEventArgs(definition));
    }

    private bool CanMoveNext()
    {
        if (IsOnLastStep)
        {
            return false;
        }

        return ValidateStep(CurrentStep);
    }

    private bool CanMovePrevious() => !IsOnFirstStep;

    private bool CanSave() => IsOnLastStep && ValidateStep(CurrentStep);

    private bool ValidateStep(ReviewWorkflowStep step)
    {
        return step switch
        {
            ReviewWorkflowStep.SelectRun => !string.IsNullOrWhiteSpace(_reviewTitle),
            ReviewWorkflowStep.ConfigureLayers => _layers.Count > 0,
            _ => true
        };
    }

    private void RaiseGuards()
    {
        _nextCommand.RaiseCanExecuteChanged();
        _backCommand.RaiseCanExecuteChanged();
        _saveCommand.RaiseCanExecuteChanged();
    }

    private void AddLayer()
    {
        var layer = new ReviewLayerViewModel("Custom layer", ReviewLayerKind.Custom, ReviewLayerDisplayMode.Custom, string.Empty, null);
        _layers.Add(layer);
        SelectedLayer = layer;
        OnPropertyChanged(nameof(OverviewLayers));
        RaiseGuards();
    }

    private void RemoveLayer()
    {
        if (SelectedLayer is null)
        {
            return;
        }

        _layers.Remove(SelectedLayer);
        SelectedLayer = _layers.LastOrDefault();
        OnPropertyChanged(nameof(OverviewLayers));
        RaiseGuards();
    }

    private void SeedDefaultLayers()
    {
        _layers.Add(new ReviewLayerViewModel(
            "Title & abstract screening",
            ReviewLayerKind.TitleAbstractScreening,
            ReviewLayerDisplayMode.Picos,
            "Title,Abstract,Keywords",
            "Screen by PICOS and mark include/exclude."));

        _layers.Add(new ReviewLayerViewModel(
            "Full-text review",
            ReviewLayerKind.FullTextScreening,
            ReviewLayerDisplayMode.Custom,
            "Title,Authors,FullText",
            "Use the PDF viewer to check inclusion."));

        _layers.Add(new ReviewLayerViewModel(
            "Data extraction",
            ReviewLayerKind.DataExtraction,
            ReviewLayerDisplayMode.Custom,
            "Outcomes,Population,Interventions",
            "Capture quantitative data for synthesis."));

        SelectedLayer = _layers.FirstOrDefault();
        OnPropertyChanged(nameof(OverviewLayers));
    }

    private void UpdateStepActivation()
    {
        foreach (var step in _steps)
        {
            step.IsActive = step.Step == CurrentStep;
        }
    }

    private ReviewProjectDefinition BuildDefinition()
    {
        var layers = _layers.Select(l => l.ToDefinition()).ToArray();
        return new ReviewProjectDefinition(
            _reviewTitle,
            _createdBy,
            _selectedLitSearchRun,
            _selectedProjectType,
            layers,
            _metadataNotes,
            DateTimeOffset.UtcNow);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
