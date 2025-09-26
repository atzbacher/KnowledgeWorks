using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models.DataExtraction;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Performs deterministic pre-processing on evidence bundles (PDF + optional XML) so the UI can present
    /// structured artefacts ahead of full data extraction.
    /// </summary>
    public interface IDataExtractionPreprocessor
    {
        Task<DataExtractionPreprocessResult> PreprocessAsync(DataExtractionPreprocessRequest request, CancellationToken ct = default);
    }
}
