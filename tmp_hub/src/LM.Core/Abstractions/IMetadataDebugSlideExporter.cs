using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions
{
    public interface IMetadataDebugSlideExporter
    {
        /// <summary>
        /// Exports each metadata item to its own slide at the given .pptx path.
        /// Returns the output path for convenience.
        /// </summary>
        Task<string> ExportAsync(IEnumerable<FileMetadata> items, string outputPath, CancellationToken ct = default);
    }
}
