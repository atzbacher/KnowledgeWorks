using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels.Pdf
{
    internal interface IPdfWebViewBridge
    {
        Task ScrollToAnnotationAsync(string annotationId, CancellationToken cancellationToken);
    }
}
