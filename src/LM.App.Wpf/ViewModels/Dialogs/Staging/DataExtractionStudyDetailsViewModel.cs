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
        private int? _siteCount;
        private string? _trialClassification;
        private bool _isRegistryStudy;
        private bool _isCohortStudy;
        private string? _geographyScope;

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

        public IReadOnlyList<string> TrialClassificationOptions { get; } = new[]
        {
            "Randomized controlled trial",
            "Non-randomized trial",
            "Meta-analysis",
            "Systematic review",
            "Observational study",
            "Other"
        };

        public IReadOnlyList<string> GeographyScopeOptions { get; } = new[]
        {
            "International",
            "National",
            "Regional",
            "Local",
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

        public int? SiteCount
        {
            get => _siteCount;
            set
            {
                if (value is < 0)
                {
                    value = 0;
                }

                SetProperty(ref _siteCount, value);
            }
        }

        public string? TrialClassification
        {
            get => _trialClassification;
            set => SetProperty(ref _trialClassification, value);
        }

        public bool IsRegistryStudy
        {
            get => _isRegistryStudy;
            set => SetProperty(ref _isRegistryStudy, value);
        }

        public bool IsCohortStudy
        {
            get => _isCohortStudy;
            set => SetProperty(ref _isCohortStudy, value);
        }

        public string? GeographyScope
        {
            get => _geographyScope;
            set => SetProperty(ref _geographyScope, value);
        }

        public void Load(HookM.DataExtractionHook? hook)
        {
            if (hook is null)
                return;

            StudyDesign = hook.StudyDesign;
            StudySetting = hook.StudySetting;
            SiteCount = hook.SiteCount;
            TrialClassification = hook.TrialClassification;
            IsRegistryStudy = hook.IsRegistryStudy.GetValueOrDefault();
            IsCohortStudy = hook.IsCohortStudy.GetValueOrDefault();
            GeographyScope = hook.GeographyScope;
        }

    }
}
