#nullable enable
using System;
using System.IO;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Metadata.EvidenceExtraction
{
    internal static class EvidenceStagingLayout
    {
        private const string RootFolder = "staging";
        private const string ExtractionFolder = "extraction";

        public static string EnsureStagingRoot(IWorkSpaceService workspace, string hash)
        {
            if (workspace is null)
                throw new ArgumentNullException(nameof(workspace));
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash must be provided.", nameof(hash));
            if (hash.Length < 8)
                throw new ArgumentException("Hash must contain at least eight characters.", nameof(hash));

            var normalized = hash.ToLowerInvariant();
            var root = workspace.GetWorkspaceRoot();
            var dir = Path.Combine(root, RootFolder, ExtractionFolder, normalized[..2], normalized[2..4], normalized);
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "tables"));
            Directory.CreateDirectory(Path.Combine(dir, "figures"));
            Directory.CreateDirectory(Path.Combine(dir, "sections"));
            return dir;
        }

        public static string NormalizeRelative(IWorkSpaceService workspace, string absolutePath)
        {
            if (workspace is null)
                throw new ArgumentNullException(nameof(workspace));
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new ArgumentException("Path must be provided.", nameof(absolutePath));

            var root = workspace.GetWorkspaceRoot();
            var relative = Path.GetRelativePath(root, absolutePath);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
