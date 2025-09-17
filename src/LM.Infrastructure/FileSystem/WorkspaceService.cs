using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.FileSystem
{
    /// <summary>
    /// Files are kept under the workspace root (shared via OneDrive/SharePoint).
    /// Metadata DB is local per workspace: %LOCALAPPDATA%\KnowledgeWorks\workspaces\<hash>\metadata.db
    /// </summary>
    public sealed class WorkspaceService : IWorkSpaceService
    {
        public string? WorkspacePath { get; private set; }

        public async Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(absoluteWorkspacePath))
                throw new ArgumentException("Workspace path must not be empty.", nameof(absoluteWorkspacePath));

            var full = Path.GetFullPath(absoluteWorkspacePath);
            Directory.CreateDirectory(full);

            // Create a place for files
            Directory.CreateDirectory(Path.Combine(full, "library"));

            WorkspacePath = full;

            // Ensure local-db directory exists
            var dbPath = GetLocalDbPath();
            var dbDir = Path.GetDirectoryName(dbPath)!;
            Directory.CreateDirectory(dbDir);

            // Simulate async (IO is sync above, but API is async)
            await Task.CompletedTask;
        }

        public string GetWorkspaceRoot()
            => WorkspacePath ?? throw new InvalidOperationException("Workspace is not set.");

        public string GetLocalDbPath()
        {
            if (WorkspacePath is null)
                throw new InvalidOperationException("Workspace is not set.");

            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.Combine(baseDir, "KnowledgeWorks", "workspaces");
            Directory.CreateDirectory(root);

            // stable, lowercase SHA1 of absolute path (short enough for folder names)
            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(WorkspacePath.ToLowerInvariant());
            var hash = BitConverter.ToString(sha1.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();

            return Path.Combine(root, hash, "metadata.db");
        }

        public string GetAbsolutePath(string relativePath)
        {
            if (WorkspacePath is null)
                throw new InvalidOperationException("Workspace is not set.");

            relativePath ??= string.Empty;
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(WorkspacePath, relativePath);
        }
    }
}
