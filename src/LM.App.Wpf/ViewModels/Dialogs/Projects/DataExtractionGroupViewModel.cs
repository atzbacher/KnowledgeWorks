using System;
using System.Collections.ObjectModel;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed class DataExtractionGroupViewModel
    {
        public DataExtractionGroupViewModel(DataExtractionGroupDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Name = definition.Name;
            Fields = new ObservableCollection<DataExtractionFieldViewModel>(CreateFields(definition));
        }

        public DataExtractionGroupDefinition Definition { get; }

        public string Name { get; }

        public ObservableCollection<DataExtractionFieldViewModel> Fields { get; }

        private static ObservableCollection<DataExtractionFieldViewModel> CreateFields(DataExtractionGroupDefinition definition)
        {
            var collection = new ObservableCollection<DataExtractionFieldViewModel>();
            foreach (var field in definition.Fields)
            {
                collection.Add(new DataExtractionFieldViewModel(field));
            }

            return collection;
        }
    }
}
