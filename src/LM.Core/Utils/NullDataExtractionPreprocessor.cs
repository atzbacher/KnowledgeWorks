#nullable enable
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models.DataExtraction;

namespace LM.Core.Utils
{
    /// <summary>
    /// Null-object implementation used when staging does not support extraction yet.
    /// </summary>
    public sealed class NullDataExtractionPreprocessor : IDataExtractionPreprocessor
    {
        public static NullDataExtractionPreprocessor Instance { get; } = new();

        private NullDataExtractionPreprocessor()
        {
        }

        public Task<DataExtractionPreprocessResult> PreprocessAsync(DataExtractionPreprocessRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(DataExtractionPreprocessResult.Empty);
        }
    }
}
