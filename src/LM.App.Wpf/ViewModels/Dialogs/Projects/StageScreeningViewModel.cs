using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed partial class StageScreeningViewModel : ObservableObject
    {
        private readonly RelayCommand _includeCommand;
        private readonly RelayCommand _excludeCommand;
        private readonly RelayCommand _resetCommand;

        public StageScreeningViewModel(ScreeningStageDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Name = definition.Name;
            Description = definition.Description;
            IncludeLabel = definition.IncludeLabel;
            ExcludeLabel = definition.ExcludeLabel;
            AllowMultipleCriteria = definition.AllowMultipleCriteria;
            AllowNotes = definition.AllowNotes;
            Criteria = new ObservableCollection<ScreeningCriterionViewModel>(CreateCriteria(definition.Criteria));

            foreach (var criterion in Criteria)
            {
                criterion.PropertyChanged += OnCriterionPropertyChanged;
            }

            _includeCommand = new RelayCommand(SetIncluded, static () => true);
            _excludeCommand = new RelayCommand(SetExcluded, static () => true);
            _resetCommand = new RelayCommand(ResetDecision, () => Decision != ScreeningStatus.Pending);
        }

        public event EventHandler? DecisionChanged;

        public ScreeningStageDefinition Definition { get; }

        public string Name { get; }

        public string Description { get; }

        public string IncludeLabel { get; }

        public string ExcludeLabel { get; }

        public bool AllowMultipleCriteria { get; }

        public bool AllowNotes { get; }

        public ObservableCollection<ScreeningCriterionViewModel> Criteria { get; }

        [ObservableProperty]
        private ScreeningStatus decision = ScreeningStatus.Pending;

        [ObservableProperty]
        private string? notes;

        public RelayCommand IncludeCommand => _includeCommand;

        public RelayCommand ExcludeCommand => _excludeCommand;

        public RelayCommand ResetCommand => _resetCommand;

        public bool IsIncluded => Decision == ScreeningStatus.Included;

        public bool IsExcluded => Decision == ScreeningStatus.Excluded;

        public bool HasDecision => Decision is ScreeningStatus.Included or ScreeningStatus.Excluded;

        partial void OnDecisionChanged(ScreeningStatus value)
        {
            _resetCommand.NotifyCanExecuteChanged();
            DecisionChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(IsIncluded));
            OnPropertyChanged(nameof(IsExcluded));
            OnPropertyChanged(nameof(HasDecision));
        }

        private void SetIncluded()
        {
            Decision = ScreeningStatus.Included;
        }

        private void SetExcluded()
        {
            Decision = ScreeningStatus.Excluded;
        }

        private void ResetDecision()
        {
            Decision = ScreeningStatus.Pending;
            Notes = null;
            foreach (var criterion in Criteria)
            {
                criterion.IsSelected = false;
            }
        }

        private IEnumerable<ScreeningCriterionViewModel> CreateCriteria(IReadOnlyList<ScreeningCriterionDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                yield return new ScreeningCriterionViewModel(definition);
            }
        }

        private void OnCriterionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!AllowMultipleCriteria && sender is ScreeningCriterionViewModel changed && e.PropertyName == nameof(ScreeningCriterionViewModel.IsSelected))
            {
                if (!changed.IsSelected)
                {
                    return;
                }

                foreach (var criterion in Criteria)
                {
                    if (!ReferenceEquals(criterion, changed))
                    {
                        criterion.IsSelected = false;
                    }
                }
            }
        }
    }
}
