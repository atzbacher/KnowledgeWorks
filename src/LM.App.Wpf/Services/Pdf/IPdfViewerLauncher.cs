namespace LM.App.Wpf.Services.Pdf
{
    internal interface IPdfViewerLauncher
    {
        void Show(string entryId, string pdfAbsolutePath, string pdfHash);
    }
}
