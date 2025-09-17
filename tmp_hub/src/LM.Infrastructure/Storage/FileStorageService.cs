using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Storage
{
    /// <summary>
    /// Writes into the current workspace safely:
    /// - lock marker (create-new)
    /// - temp copy
    /// - atomic move
    /// </summary>
    public sealed class FileStorageService : IFileStorageRepository
    {
        private readonly IWorkSpaceService _ws;

        public FileStorageService(IWorkSpaceService workspace)
        {
            _ws = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<string> SaveNewAsync(string sourcePath, string relativeTargetDir, string? preferredFileName = null, CancellationToken ct = default)
        {
            var root = _ws.GetWorkspaceRoot(); // throws if not set
            var fileName = preferredFileName ?? Path.GetFileName(sourcePath);
            var safeName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

            var relDir = (relativeTargetDir ?? "").Trim().TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var absDir = Path.Combine(root, relDir);
            Directory.CreateDirectory(absDir);

            var targetPath = Path.Combine(absDir, safeName);
            targetPath = EnsureUniquePath(targetPath);

            var lockPath = targetPath + ".lock.json";
            using var lockFs = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(lockFs, new { createdUtc = DateTime.UtcNow, machine = Environment.MachineName }, cancellationToken: ct);
            await lockFs.FlushAsync(ct);
            lockFs.Close();

            try
            {
                var tmp = targetPath + ".tmp";
                File.Copy(sourcePath, tmp, overwrite: true);
                File.Move(tmp, targetPath);
            }
            finally
            {
                try { File.Delete(lockPath); } catch { /* ignore */ }
            }

            // Return workspace-relative path
            var rel = Path.GetRelativePath(root, targetPath);
            return rel.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (int i = 1; ; i++)
            {
                var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
        }
    }
}
