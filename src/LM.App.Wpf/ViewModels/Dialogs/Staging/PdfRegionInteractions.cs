#nullable enable

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed record PdfRegionDraft(int PageNumber, double X, double Y, double Width, double Height);

    internal sealed record PdfRegionUpdate(DataExtractionRegionViewModel Region,
                                           int PageNumber,
                                           double X,
                                           double Y,
                                           double Width,
                                           double Height);
}
