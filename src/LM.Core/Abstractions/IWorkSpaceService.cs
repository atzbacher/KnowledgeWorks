using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    /// <summary>
    /// Tracks the current workspace and provides shared paths.
    /// Files are stored in the workspace folder; metadata DB is local per workspace.
    /// </summary>
    public interface IWorkSpaceService
    {
        /// <summary>Absolute path to the current workspace folder (or null if not set).</summary>
        string? WorkspacePath { get; }

        /// <summary>Returns the absolute path to the workspace folder (throws if not set).</summary>
        string GetWorkspaceRoot();

        /// <summary>Returns a workspace-specific local DB file path (absolute).</summary>
        string GetLocalDbPath();

        /// <summary>Returns an absolute path given a workspace-relative path.</summary>
        string GetAbsolutePath(string relativePath);

        /// <summary>Ensure workspace folder exists, create basic subfolders (e.g., 'library'), and switch active workspace.</summary>
        Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default);
    }
}
