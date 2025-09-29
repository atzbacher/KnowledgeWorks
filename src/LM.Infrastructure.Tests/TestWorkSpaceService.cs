using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Tests
{
    internal sealed class TestWorkSpaceService : IWorkSpaceService
    {
        public TestWorkSpaceService(string workspacePath)
        {
            WorkspacePath = workspacePath ?? throw new ArgumentNullException(nameof(workspacePath));
        }

        public string? WorkspacePath { get; private set; }

        public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
        {
            WorkspacePath = absoluteWorkspacePath ?? throw new ArgumentNullException(nameof(absoluteWorkspacePath));
            Directory.CreateDirectory(WorkspacePath);
            return Task.CompletedTask;
        }

        public string GetAbsolutePath(string relativePath)
        {
            if (WorkspacePath is null)
            {
                throw new InvalidOperationException("WorkspacePath is not set.");
            }

            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            return Path.Combine(WorkspacePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public string GetLocalDbPath()
        {
            if (WorkspacePath is null)
            {
                throw new InvalidOperationException("WorkspacePath is not set.");
            }

            return Path.Combine(WorkspacePath, "workspace.db");
        }

        public string GetWorkspaceRoot()
        {
            if (WorkspacePath is null)
            {
                throw new InvalidOperationException("WorkspacePath is not set.");
            }

            return WorkspacePath;
        }
    }
}
