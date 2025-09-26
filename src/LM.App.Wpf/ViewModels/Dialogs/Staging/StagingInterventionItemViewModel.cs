#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingInterventionItemViewModel : ObservableObject
    {
        public StagingInterventionItemViewModel(HookM.DataExtractionIntervention intervention)
        {
            Intervention = intervention;
            Name = string.IsNullOrWhiteSpace(intervention.Name) ? intervention.Id : intervention.Name;
        }

        public HookM.DataExtractionIntervention Intervention { get; }

        public string Id => Intervention.Id;

        public string Name { get; }
    }
}
