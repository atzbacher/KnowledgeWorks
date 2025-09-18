using System;
using System.IO;
using System.Security.Cryptography;
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
            _ = relativeTargetDir; // legacy parameter retained for compatibility; path now determined by hash.
            var hash = await ComputeFileHashAsync(sourcePath, ct);

            var relDir = Path.Combine("library", hash[..2], hash[2..4]);
            var absDir = Path.Combine(root, relDir);
            Directory.CreateDirectory(absDir);

            var extension = Path.GetExtension(preferredFileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = Path.GetExtension(sourcePath);
            var storedName = string.IsNullOrWhiteSpace(extension) ? hash : $"{hash}{extension}";

            var targetPath = Path.Combine(absDir, storedName);
            if (File.Exists(targetPath))
            {
                var existingRel = Path.Combine(relDir, storedName);
                return existingRel.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

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
            var rel = Path.Combine(relDir, storedName);
            return rel.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var bytes = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
