#nullable enable

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.ViewModels;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingFigureViewModel : ObservableObject
    {
        private HookM.DataExtractionFigure _hook;

        public StagingFigureViewModel(HookM.DataExtractionFigure hook, StagingEvidencePreview.FigurePreview? preview)
        {
            _hook = hook ?? throw new ArgumentNullException(nameof(hook));
            Title = string.IsNullOrWhiteSpace(hook.Title) ? preview?.Caption ?? "Figure" : hook.Title;
            Caption = preview?.Caption ?? hook.Caption ?? string.Empty;
            Pages = string.Join(", ", hook.Pages);
        }

        public string Id => _hook.Id;

        public string Title { get; }

        public string Caption { get; }

        public string Pages { get; }

        public string SourcePath => _hook.SourcePath ?? string.Empty;

        public string ProvenanceHash => _hook.ProvenanceHash ?? string.Empty;

        public void Update(HookM.DataExtractionFigure hook)
        {
            _hook = hook ?? throw new ArgumentNullException(nameof(hook));
            OnPropertyChanged(nameof(SourcePath));
            OnPropertyChanged(nameof(ProvenanceHash));
        }
    }
}
