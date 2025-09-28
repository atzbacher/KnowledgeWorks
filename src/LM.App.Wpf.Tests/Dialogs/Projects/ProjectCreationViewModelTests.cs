using System.Collections.Generic;
using LM.App.Wpf.ViewModels.Dialogs.Projects;
using LM.Review.Core.Models;
using Xunit;

namespace LM.App.Wpf.Tests.Dialogs.Projects
{
    public sealed class ProjectCreationViewModelTests
    {
        private static ProjectCreationRequest CreateRequest()
        {
            var document = new ScreeningDocumentDefinition(
                "Example study",
                "Abstract",
                new[] { "Doe J.", "Smith A." },
                new[] { new DocumentAttributeDefinition("Journal", "Example"), new DocumentAttributeDefinition("Year", "2024") },
                new[] { "cardiology" });

            var stage = new ScreeningStageDefinition(
                "Stage",
                "Description",
                "Include",
                "Exclude",
                true,
                true,
                new[]
                {
                    new ScreeningCriterionDefinition("p", "Population", null, false)
                });

            var pdf = new List<PdfPagePreviewDefinition>
            {
                new PdfPagePreviewDefinition(1, "Cover", "Summary")
            };

            var groups = new List<DataExtractionGroupDefinition>
            {
                new DataExtractionGroupDefinition(
                    "Group",
                    new[]
                    {
                        new DataExtractionFieldDefinition("field", "Field", ProjectCreationFieldTemplates.Text, null, null, null, false)
                    })
            };

            return new ProjectCreationRequest("Project", document, stage, stage, pdf, groups);
        }

        [Fact]
        public void DataExtractionVisibility_FollowsStageDecisions()
        {
            var viewModel = new ProjectCreationViewModel(CreateRequest());

            Assert.False(viewModel.IsDataExtractionVisible);

            viewModel.TitleAbstractStage.IncludeCommand.Execute(null);
            Assert.False(viewModel.IsDataExtractionVisible);

            viewModel.FullTextStage.IncludeCommand.Execute(null);
            Assert.True(viewModel.IsDataExtractionVisible);

            viewModel.FullTextStage.ExcludeCommand.Execute(null);
            Assert.False(viewModel.IsDataExtractionVisible);
        }

        [Fact]
        public void SaveCommandRequiresDecisions()
        {
            var viewModel = new ProjectCreationViewModel(CreateRequest());

            Assert.False(viewModel.SaveCommand.CanExecute(null));

            viewModel.TitleAbstractStage.IncludeCommand.Execute(null);
            viewModel.FullTextStage.IncludeCommand.Execute(null);

            Assert.True(viewModel.SaveCommand.CanExecute(null));
        }
    }
}
