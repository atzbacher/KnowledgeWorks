using System;
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record ProjectCreationRequest
    {
        public ProjectCreationRequest(
            string projectName,
            ScreeningDocumentDefinition document,
            ScreeningStageDefinition titleAbstractStage,
            ScreeningStageDefinition fullTextStage,
            IEnumerable<PdfPagePreviewDefinition> fullTextPdfPages,
            IEnumerable<DataExtractionGroupDefinition> dataExtractionGroups)
        {
            ProjectName = Normalize(projectName, nameof(projectName));
            Document = document ?? throw new ArgumentNullException(nameof(document));
            TitleAbstractStage = titleAbstractStage ?? throw new ArgumentNullException(nameof(titleAbstractStage));
            FullTextStage = fullTextStage ?? throw new ArgumentNullException(nameof(fullTextStage));
            FullTextPdfPages = CreatePages(fullTextPdfPages);
            DataExtractionGroups = CreateGroups(dataExtractionGroups);
        }

        public string ProjectName { get; }

        public ScreeningDocumentDefinition Document { get; }

        public ScreeningStageDefinition TitleAbstractStage { get; }

        public ScreeningStageDefinition FullTextStage { get; }

        public IReadOnlyList<PdfPagePreviewDefinition> FullTextPdfPages { get; }

        public IReadOnlyList<DataExtractionGroupDefinition> DataExtractionGroups { get; }

        private static string Normalize(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"A non-empty value for '{argumentName}' is required.", argumentName);

            return value.Trim();
        }

        private static IReadOnlyList<PdfPagePreviewDefinition> CreatePages(IEnumerable<PdfPagePreviewDefinition> pages)
        {
            if (pages is null)
                throw new ArgumentNullException(nameof(pages));

            var list = new List<PdfPagePreviewDefinition>();
            foreach (var page in pages)
            {
                if (page is null)
                    throw new ArgumentException("PDF preview collections cannot contain null entries.", nameof(pages));

                list.Add(page);
            }

            return list.AsReadOnly();
        }

        private static IReadOnlyList<DataExtractionGroupDefinition> CreateGroups(IEnumerable<DataExtractionGroupDefinition> groups)
        {
            if (groups is null)
                throw new ArgumentNullException(nameof(groups));

            var list = new List<DataExtractionGroupDefinition>();
            foreach (var group in groups)
            {
                if (group is null)
                    throw new ArgumentException("Data extraction group collections cannot contain null entries.", nameof(groups));

                list.Add(group);
            }

            return list.AsReadOnly();
        }
    }
}
