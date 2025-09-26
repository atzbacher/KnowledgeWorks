#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LM.App.Wpf.ViewModels;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingEndpointsTabViewModel : StagingTabViewModel
    {
        public StagingEndpointsTabViewModel()
            : base("Endpoints")
        {
        }

        public ObservableCollection<StagingEndpointViewModel> Endpoints { get; } = new();

        protected override void OnItemUpdated(StagingItem? item)
        {
            Endpoints.Clear();

            if (item?.DataExtractionHook is null)
                return;

            var hook = item.DataExtractionHook;
            var populationLookup = hook.Populations.ToDictionary(p => p.Id, p => p.Label ?? p.Id, StringComparer.OrdinalIgnoreCase);
            var interventionLookup = hook.Interventions.ToDictionary(i => i.Id, i => i.Name ?? i.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var endpoint in hook.Endpoints)
            {
                if (endpoint is null)
                    continue;

                var populations = endpoint.PopulationIds
                    .Select(id => populationLookup.TryGetValue(id, out var label) ? label : id)
                    .ToList();
                var interventions = endpoint.InterventionIds
                    .Select(id => interventionLookup.TryGetValue(id, out var label) ? label : id)
                    .ToList();

                var viewModel = new StagingEndpointViewModel(endpoint, populations, interventions, OnEndpointStateChanged);
                Endpoints.Add(viewModel);
            }
        }

        protected override void RefreshValidation()
        {
            if (Item is null)
            {
                SetValidationMessages(new[] { "Select a staged item to review endpoints." });
                return;
            }

            if (Endpoints.Count == 0)
            {
                SetValidationMessages(Array.Empty<string>());
                return;
            }

            var messages = new List<string>();
            if (Endpoints.Any(static e => !e.IsConfirmed))
                messages.Add("Confirm extracted endpoints before committing.");

            SetValidationMessages(messages);
        }

        private void OnEndpointStateChanged(StagingEndpointViewModel viewModel)
        {
            if (Item?.DataExtractionHook is null)
                return;

            var endpoints = Item.DataExtractionHook.Endpoints;
            var index = endpoints.FindIndex(e => e.Id == viewModel.Id);
            if (index < 0)
                return;

            var source = endpoints[index];
            var updated = new HookM.DataExtractionEndpoint
            {
                Id = source.Id,
                Name = source.Name,
                Description = source.Description,
                Timepoint = source.Timepoint,
                Measure = source.Measure,
                PopulationIds = new List<string>(source.PopulationIds),
                InterventionIds = new List<string>(source.InterventionIds),
                ResultSummary = source.ResultSummary,
                EffectSize = source.EffectSize,
                Notes = source.Notes,
                Confirmed = viewModel.IsConfirmed
            };

            endpoints[index] = updated;
            viewModel.UpdateEndpoint(updated);
            RefreshValidation();
        }
    }
}
