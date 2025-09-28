using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed partial class ScreeningCriterionViewModel : ObservableObject
    {
        public ScreeningCriterionViewModel(ScreeningCriterionDefinition definition)
        {
            Definition = definition;
            Key = definition.Key;
            Label = definition.Label;
            Description = definition.Description;
            isSelected = definition.IsDefaultSelected;
        }

        public ScreeningCriterionDefinition Definition { get; }

        public string Key { get; }

        public string Label { get; }

        public string? Description { get; }

        [ObservableProperty]
        private bool isSelected;
    }
}
