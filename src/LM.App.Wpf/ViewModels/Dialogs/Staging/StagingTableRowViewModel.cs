#nullable enable

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.ViewModels;
using LM.Core.Models.DataExtraction;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingTableRowViewModel : ObservableObject
    {
        private readonly Action<StagingTableRowViewModel> _classificationChanged;
        private TableClassificationKind _classification;
        private HookM.DataExtractionTable _hook;

        public StagingTableRowViewModel(HookM.DataExtractionTable hook,
                                        StagingEvidencePreview.TablePreview? preview,
                                        Action<StagingTableRowViewModel> classificationChanged)
        {
            _hook = hook ?? throw new ArgumentNullException(nameof(hook));
            _classificationChanged = classificationChanged ?? throw new ArgumentNullException(nameof(classificationChanged));

            Title = string.IsNullOrWhiteSpace(hook.Title) ? preview?.Title ?? "Table" : hook.Title;
            Populations = preview?.Populations ?? Array.Empty<string>();
            Endpoints = preview?.Endpoints ?? Array.Empty<string>();
            Pages = preview?.Pages ?? Array.Empty<int>();

            _classification = TryParseClassification(hook.Caption, preview?.Classification) ?? TableClassificationKind.Unknown;
        }

        public string Id => _hook.Id;

        public string Title { get; }

        public IReadOnlyList<string> Populations { get; }

        public IReadOnlyList<string> Endpoints { get; }

        public IReadOnlyList<int> Pages { get; }

        public string PopulationSummary => string.Join(", ", Populations);

        public string EndpointSummary => string.Join(", ", Endpoints);

        public string PageSummary => string.Join(", ", Pages);

        public string SourcePath => _hook.SourcePath ?? string.Empty;

        public string ProvenanceHash => _hook.ProvenanceHash ?? string.Empty;

        public TableClassificationKind Classification
        {
            get => _classification;
            set
            {
                if (SetProperty(ref _classification, value))
                    _classificationChanged(this);
            }
        }

        public void UpdateHook(HookM.DataExtractionTable updated)
        {
            _hook = updated ?? throw new ArgumentNullException(nameof(updated));
            OnPropertyChanged(nameof(SourcePath));
            OnPropertyChanged(nameof(ProvenanceHash));
        }

        public HookM.DataExtractionTable Snapshot => _hook;

        private static TableClassificationKind? TryParseClassification(string? caption, TableClassificationKind? fallback)
        {
            if (!string.IsNullOrWhiteSpace(caption) && Enum.TryParse<TableClassificationKind>(caption, true, out var parsed))
                return parsed;

            return fallback;
        }
    }
}
