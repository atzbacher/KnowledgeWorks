#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using LM.App.Wpf.ViewModels;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class StagingFiguresTabViewModel : StagingTabViewModel
    {
        private StagingFigureViewModel? _selected;

        public StagingFiguresTabViewModel()
            : base("Figures")
        {
        }

        public ObservableCollection<StagingFigureViewModel> Figures { get; } = new();

        public StagingFigureViewModel? Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        protected override void OnItemUpdated(StagingItem? item)
        {
            Figures.Clear();
            Selected = null;

            if (item is null)
                return;

            item.DataExtractionHook ??= new HookM.DataExtractionHook
            {
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = Environment.UserName ?? "unknown"
            };

            var hookFigures = item.DataExtractionHook.Figures;
            var previewFigures = item.EvidencePreview?.Figures ?? Array.Empty<StagingEvidencePreview.FigurePreview>();

            for (var i = 0; i < Math.Max(hookFigures.Count, previewFigures.Count); i++)
            {
                if (i >= hookFigures.Count)
                    hookFigures.Add(new HookM.DataExtractionFigure());

                var hook = hookFigures[i];
                var preview = previewFigures.ElementAtOrDefault(i);
                var viewModel = new StagingFigureViewModel(hook, preview);
                Figures.Add(viewModel);
            }

            Selected = Figures.FirstOrDefault();
        }

        protected override void RefreshValidation()
        {
            if (Item is null)
            {
                SetValidationMessages(new[] { "Select a staged item to inspect figures." });
                return;
            }

            SetValidationMessages(Array.Empty<string>());
        }
    }
}
