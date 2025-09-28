using System;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record PdfPagePreviewDefinition
    {
        public PdfPagePreviewDefinition(int pageNumber, string caption, string summary)
        {
            if (pageNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page numbers must be greater than zero.");

            PageNumber = pageNumber;
            Caption = caption?.Trim() ?? string.Empty;
            Summary = summary?.Trim() ?? string.Empty;
        }

        public int PageNumber { get; }

        public string Caption { get; }

        public string Summary { get; }
    }
}
