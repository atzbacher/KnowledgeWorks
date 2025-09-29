using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions;

public interface IPdfAnnotationPreviewStorage
{
    Task<string> SaveAsync(string pdfHash, string annotationId, byte[] pngBytes, CancellationToken cancellationToken);
}
