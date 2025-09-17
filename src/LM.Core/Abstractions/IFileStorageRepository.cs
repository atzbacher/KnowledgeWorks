using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Responsible for safe writes to the shared folder (lock markers + atomic rename).
    /// </summary>
    public interface IFileStorageRepository
    {
        /// <summary>
        /// Save a file into the shared storage under a relative directory.
        /// Returns the relative path that was written.
        /// </summary>
        Task<string> SaveNewAsync(string sourcePath, string relativeTargetDir, string? preferredFileName = null, CancellationToken ct = default);
    }
}
