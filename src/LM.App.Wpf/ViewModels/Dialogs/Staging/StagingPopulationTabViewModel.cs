#nullable enable

using System;
using System.Collections.ObjectModel;
using LM.App.Wpf.ViewModels;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingPopulationTabViewModel : StagingTabViewModel
    {
        public StagingPopulationTabViewModel()
            : base("Population")
        {
        }

        public ObservableCollection<StagingPopulationItemViewModel> Populations { get; } = new();

        public ObservableCollection<StagingInterventionItemViewModel> Interventions { get; } = new();

        protected override void OnItemUpdated(StagingItem? item)
        {
            Populations.Clear();
            Interventions.Clear();

            if (item?.DataExtractionHook is null)
                return;

            foreach (var population in item.DataExtractionHook.Populations)
            {
                if (population is null)
                    continue;
                Populations.Add(new StagingPopulationItemViewModel(population));
            }

            foreach (var intervention in item.DataExtractionHook.Interventions)
            {
                if (intervention is null)
                    continue;
                Interventions.Add(new StagingInterventionItemViewModel(intervention));
            }
        }

        protected override void RefreshValidation()
        {
            if (Item is null)
            {
                SetValidationMessages(new[] { "Select a staged item to inspect population details." });
                return;
            }

            var issues = Populations.Count == 0
                ? new[] { "No populations detected; confirm extraction coverage." }
                : Array.Empty<string>();

            SetValidationMessages(issues);
        }
    }
}
