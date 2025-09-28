using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed partial class DataExtractionFieldViewModel : ObservableObject
    {
        public DataExtractionFieldViewModel(DataExtractionFieldDefinition definition)
        {
            Definition = definition;
            Key = definition.Key;
            Label = definition.Label;
            TemplateKey = definition.TemplateKey;
            Placeholder = definition.Placeholder;
            Options = new ObservableCollection<string>(definition.Options);
            isRequired = definition.IsRequired;
            value = definition.DefaultValue;
        }

        public DataExtractionFieldDefinition Definition { get; }

        public string Key { get; }

        public string Label { get; }

        public string TemplateKey { get; }

        public string? Placeholder { get; }

        public ObservableCollection<string> Options { get; }

        public bool HasOptions => Options.Count > 0;

        [ObservableProperty]
        private string? value;

        [ObservableProperty]
        private bool isRequired;
    }
}
