using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    public interface IPdfAnnotationOverlayReader
    {
        Task<string?> GetOverlayJsonAsync(string pdfHash, CancellationToken cancellationToken = default);
    }
}
