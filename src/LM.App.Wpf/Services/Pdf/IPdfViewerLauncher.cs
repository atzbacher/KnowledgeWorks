namespace LM.App.Wpf.Services.Pdf
{
    public interface IPdfViewerLauncher
    {
        void Show(string entryId, string pdfAbsolutePath, string pdfHash);
    }
}
