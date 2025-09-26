#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    public interface IDataExtractionPowerPointExporter
    {
        Task<bool> CanExportAsync(string entryId, CancellationToken ct = default);

        Task<string> ExportAsync(string entryId, string outputPath, CancellationToken ct = default);
    }
}
