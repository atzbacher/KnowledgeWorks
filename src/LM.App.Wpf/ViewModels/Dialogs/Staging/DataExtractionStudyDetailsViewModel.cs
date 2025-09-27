#nullable enable

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class DataExtractionStudyDetailsViewModel : ObservableObject
    {
        private string? _studyDesign;
        private string? _studySetting;

        public DataExtractionStudyDetailsViewModel()
        {
        }

        public IReadOnlyList<string> StudyDesignOptions { get; } = new[]
        {
            "Randomized controlled trial",
            "Retrospective cohort",
            "Prospective cohort",
            "Meta-analysis",
            "Systematic review",
            "Case series"
        };

        public IReadOnlyList<string> StudySettingOptions { get; } = new[]
        {
            "Multicenter",
            "Single center",
            "Registry",
            "Other"
        };

        public string? StudyDesign
        {
            get => _studyDesign;
            set => SetProperty(ref _studyDesign, value);
        }

        public string? StudySetting
        {
            get => _studySetting;
            set => SetProperty(ref _studySetting, value);
        }

        public void Load(HookM.DataExtractionHook? hook)
        {
            if (hook is null)
                return;

            StudyDesign = hook.StudyDesign;
            StudySetting = hook.StudySetting;
        }

    }
}
