using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs.Projects;

namespace LM.App.Wpf.ViewModels
{
    internal sealed partial class ShellViewModel : ObservableObject
    {
        private readonly IDialogService _dialogService;
        private readonly RelayCommand _createProjectCommand;

        public ShellViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _createProjectCommand = new RelayCommand(OpenProjectCreation);
        }

        public RelayCommand CreateProjectCommand => _createProjectCommand;

        private void OpenProjectCreation()
        {
            var request = BuildSampleRequest();
            _dialogService.ShowProjectCreation(request);
        }

        private static ProjectCreationRequest BuildSampleRequest()
        {
            var document = new ScreeningDocumentDefinition(
                "When surgery is not an option: case report of transcatheter valve-in-valve replacement for mitral valve dysfunction",
                "Bioprosthetic heart valve dysfunction remains a challenge for frail patients with high surgical risk. This rapid assessment reviews transcatheter valve-in-valve replacement as an alternative, summarizing outcomes and peri-procedural considerations.",
                new[]
                {
                    "Alsancak Y.",
                    "Kani H.",
                    "Gürbüz A.S.",
                    "Aydın M.F."
                },
                new[]
                {
                    new DocumentAttributeDefinition("Journal", "Literature Medicine"),
                    new DocumentAttributeDefinition("Published", "Dec 2023"),
                    new DocumentAttributeDefinition("DOI", "10.1000/example-doi-2023")
                },
                new[]
                {
                    "transcatheter", "valve-in-valve", "mitral", "structural heart"
                });

            var titleStage = new ScreeningStageDefinition(
                "Title & abstract screening",
                "Decide whether the study meets the PICOS criteria based on bibliographic information.",
                "Include",
                "Exclude",
                true,
                true,
                new[]
                {
                    new ScreeningCriterionDefinition("population", "Population mismatch", "The study population diverges from the specified patient cohort.", false),
                    new ScreeningCriterionDefinition("intervention", "Intervention mismatch", "Procedure or therapy does not evaluate transcatheter valve-in-valve approaches.", false),
                    new ScreeningCriterionDefinition("outcomes", "Outcomes insufficient", "No relevant clinical endpoints are reported.", false),
                    new ScreeningCriterionDefinition("language", "Language restriction", "Full text unavailable in the review language.", false)
                });

            var fullTextStage = new ScreeningStageDefinition(
                "Full text review",
                "Confirm eligibility against full inclusion criteria and document any exclusion rationale.",
                "Include",
                "Exclude",
                false,
                true,
                new[]
                {
                    new ScreeningCriterionDefinition("study-design", "Study design mismatch", "Does not meet accepted design types (RCT, observational, case series).", false),
                    new ScreeningCriterionDefinition("picos", "PICOS deviation", "Fails to satisfy PICOS specifications in detail.", false),
                    new ScreeningCriterionDefinition("insufficient-data", "Insufficient data", "Missing quantitative outcomes or follow-up details.", false)
                });

            var pdfPages = new List<PdfPagePreviewDefinition>
            {
                new PdfPagePreviewDefinition(1, "Abstract & summary", "The introductory section outlines mitral valve dysfunction cases treated with a transcatheter valve-in-valve approach, including key hemodynamic outcomes."),
                new PdfPagePreviewDefinition(2, "Procedural details", "Illustrations highlight catheter positioning, valve deployment, and peri-procedural imaging guidance."),
                new PdfPagePreviewDefinition(3, "Clinical outcomes", "Table summarises survival, rehospitalisation, and post-operative complications across the presented cohort.")
            };

            var dataExtractionGroups = new List<DataExtractionGroupDefinition>
            {
                new DataExtractionGroupDefinition(
                    "Study profile",
                    new[]
                    {
                        new DataExtractionFieldDefinition("design", "Study design", ProjectCreationFieldTemplates.Choice, "Select the study design", new[] { "Case report", "Case series", "Prospective cohort", "Randomised trial" }, "Case report", true),
                        new DataExtractionFieldDefinition("sample-size", "Sample size", ProjectCreationFieldTemplates.Text, "Enter the number of participants", null, null, true),
                        new DataExtractionFieldDefinition("setting", "Clinical setting", ProjectCreationFieldTemplates.Text, "Hospital type or region", null, null, false)
                    }),
                new DataExtractionGroupDefinition(
                    "Primary outcomes",
                    new[]
                    {
                        new DataExtractionFieldDefinition("primary-outcome", "Primary outcome", ProjectCreationFieldTemplates.MultiLine, "Summarise mortality, valve gradients, and rehospitalisation", null, null, true),
                        new DataExtractionFieldDefinition("follow-up", "Follow-up duration", ProjectCreationFieldTemplates.Text, "Specify median or mean follow-up period", null, null, false)
                    }),
                new DataExtractionGroupDefinition(
                    "Secondary notes",
                    new[]
                    {
                        new DataExtractionFieldDefinition("complications", "Notable complications", ProjectCreationFieldTemplates.MultiLine, "Record periprocedural complications or device malfunctions", null, null, false),
                        new DataExtractionFieldDefinition("remarks", "Reviewer remarks", ProjectCreationFieldTemplates.MultiLine, "Add contextual insights or escalation notes", null, null, false)
                    })
            };

            return new ProjectCreationRequest(
                "Structural valve intervention project",
                document,
                titleStage,
                fullTextStage,
                pdfPages,
                dataExtractionGroups);
        }
    }
}
