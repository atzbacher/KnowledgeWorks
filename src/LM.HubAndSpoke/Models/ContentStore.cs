#nullable enable
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.Abstractions;

namespace LM.HubSpoke.Storage
{
    internal sealed class ContentStore
    {
        private readonly IWorkSpaceService _ws;
        private readonly IHasher _hasher;

        public ContentStore(IWorkSpaceService ws, IHasher hasher)
        {
            _ws = ws; _hasher = hasher;
        }

        public async Task<CasResult> MoveToCasAsync(string abs, CancellationToken ct)
        {
            if (!File.Exists(abs)) return new CasResult(null, null, 0, null, null);

            var sha = await _hasher.ComputeSha256Async(abs, ct);
            var ext = Path.GetExtension(abs);
            var a = sha.Substring(0, 2);
            var b = sha.Substring(2, 2);
            var relDir = Path.Combine("library", a, b).Replace('\\', '/');
            var relPath = Path.Combine(relDir, $"{sha}{ext}").Replace('\\', '/');
            var absTarget = _ws.GetAbsolutePath(relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absTarget)!);

            if (!File.Exists(absTarget))
            {
                var wsRoot = _ws.GetWorkspaceRoot();
                var wsFull = Path.GetFullPath(wsRoot);
                if (!wsFull.EndsWith(Path.DirectorySeparatorChar)) wsFull += Path.DirectorySeparatorChar;
                var srcFull = Path.GetFullPath(abs);
                var underWorkspace = srcFull.StartsWith(wsFull, System.StringComparison.OrdinalIgnoreCase);

                try { if (underWorkspace) File.Move(abs, absTarget); else File.Copy(abs, absTarget); }
                catch { if (!File.Exists(absTarget)) File.Copy(abs, absTarget, overwrite: false); }
            }

            var fi = new FileInfo(absTarget);
            return new CasResult(
                RelPath: relPath,
                Sha: sha,
                Bytes: fi.Exists ? fi.Length : 0,
                Mime: MimeFromExt(ext),
                Original: Path.GetFileName(abs)
            );
        }

        public static string MimeFromExt(string ext) => ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
