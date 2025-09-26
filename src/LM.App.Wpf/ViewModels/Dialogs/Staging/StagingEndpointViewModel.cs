#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingEndpointViewModel : ObservableObject
    {
        private readonly Action<StagingEndpointViewModel> _stateChanged;
        private bool _confirmed;

        public StagingEndpointViewModel(HookM.DataExtractionEndpoint endpoint,
                                        IReadOnlyList<string> populationLabels,
                                        IReadOnlyList<string> interventionLabels,
                                        Action<StagingEndpointViewModel> stateChanged)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _stateChanged = stateChanged ?? throw new ArgumentNullException(nameof(stateChanged));

            Name = string.IsNullOrWhiteSpace(endpoint.Name) ? "Endpoint" : endpoint.Name;
            Populations = string.Join(", ", populationLabels ?? Array.Empty<string>());
            Interventions = string.Join(", ", interventionLabels ?? Array.Empty<string>());
            _confirmed = endpoint.Confirmed;
        }

        public HookM.DataExtractionEndpoint Endpoint { get; private set; }

        public string Id => Endpoint.Id;

        public string Name { get; }

        public string Populations { get; }

        public string Interventions { get; }

        public bool IsConfirmed
        {
            get => _confirmed;
            set
            {
                if (SetProperty(ref _confirmed, value))
                    _stateChanged(this);
            }
        }

        public void UpdateEndpoint(HookM.DataExtractionEndpoint endpoint)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }
    }
}
