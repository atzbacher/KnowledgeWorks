using System.Collections.Generic;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.HubSpoke.Models;
using Xunit;

namespace LM.App.Wpf.Tests.Dialogs.Staging
{
    public sealed class StagingEndpointsTabViewModelTests
    {
        [Fact]
        public void Confirming_Endpoint_Updates_Model()
        {
            var hook = new DataExtractionHook
            {
                Populations = new List<DataExtractionPopulation>
                {
                    new DataExtractionPopulation { Id = "p1", Label = "Adults" }
                },
                Interventions = new List<DataExtractionIntervention>
                {
                    new DataExtractionIntervention { Id = "i1", Name = "Drug A" }
                },
                Endpoints = new List<DataExtractionEndpoint>
                {
                    new DataExtractionEndpoint
                    {
                        Id = "e1",
                        Name = "Mortality",
                        PopulationIds = new List<string> { "p1" },
                        InterventionIds = new List<string> { "i1" },
                        Confirmed = false
                    }
                }
            };

            var item = new StagingItem
            {
                DataExtractionHook = hook
            };

            var viewModel = new StagingEndpointsTabViewModel();
            viewModel.Update(item);

            var endpoint = Assert.Single(viewModel.Endpoints);
            Assert.False(endpoint.IsConfirmed);

            endpoint.IsConfirmed = true;

            Assert.True(item.DataExtractionHook!.Endpoints[0].Confirmed);
        }
    }
}
