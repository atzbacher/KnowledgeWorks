namespace LM.App.Wpf.ViewModels.Pdf
{
    /// <summary>
    /// Represents the payload passed from XAML menu items to the
    /// <see cref="PdfViewerViewModel.ChangeAnnotationColorCommand"/>.
    /// </summary>
    public sealed class PdfAnnotationColorCommandParameter
    {
        public PdfAnnotation? Annotation { get; set; }

        public string? ColorName { get; set; }
    }
}
