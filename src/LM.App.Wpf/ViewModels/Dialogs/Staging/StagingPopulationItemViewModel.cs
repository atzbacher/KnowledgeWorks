#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingPopulationItemViewModel : ObservableObject
    {
        public StagingPopulationItemViewModel(HookM.DataExtractionPopulation population)
        {
            Population = population;
            Label = string.IsNullOrWhiteSpace(population.Label) ? population.Id : population.Label;
        }

        public HookM.DataExtractionPopulation Population { get; }

        public string Id => Population.Id;

        public string Label { get; }
    }
}
