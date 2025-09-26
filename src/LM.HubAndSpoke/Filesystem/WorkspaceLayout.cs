#nullable enable
using System;
using System.IO;
using LM.Core.Abstractions;

namespace LM.HubSpoke.FileSystem
{
    internal static class WorkspaceLayout
    {
        public static void Ensure(IWorkSpaceService ws)
        {
            Directory.CreateDirectory(EntriesRoot(ws));
            Directory.CreateDirectory(StorageRoot(ws));
            Directory.CreateDirectory(ExtractionRoot(ws));
        }

        public static string EntriesRoot(IWorkSpaceService ws) => Path.Combine(ws.GetWorkspaceRoot(), "entries");
        public static string StorageRoot(IWorkSpaceService ws) => Path.Combine(ws.GetWorkspaceRoot(), "library");
        public static string ExtractionRoot(IWorkSpaceService ws) => Path.Combine(ws.GetWorkspaceRoot(), "extraction");

        public static string EntryDir(IWorkSpaceService ws, string id) => Path.Combine(EntriesRoot(ws), id);

        public static string HubPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hub.json");
        public static string ArticleHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "article.json");
        public static string DocumentHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "document.json");
        public static string LitSearchHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "litsearch.json");
        public static string AttachmentsHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "attachments.json");
        public static string NotesHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "notes.json");

        public static string DataExtractionRelativePath(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash must be provided.", nameof(hash));
            if (hash.Length < 4)
                throw new ArgumentException("Hash must be at least 4 characters long.", nameof(hash));

            var normalized = hash.ToLowerInvariant();
            var prefixed = normalized.StartsWith("sha256-", StringComparison.Ordinal)
                ? normalized
                : $"sha256-{normalized}";

            var dashIndex = prefixed.IndexOf('-');
            if (dashIndex < 0 || prefixed.Length - (dashIndex + 1) < 4)
                throw new ArgumentException("Hash must contain at least four hexadecimal characters.", nameof(hash));

            var pureHash = prefixed[(dashIndex + 1)..];
            var dir = Path.Combine("extraction", pureHash[..2], pureHash[2..4]);
            return Path.Combine(dir, $"{prefixed}.json");
        }

        public static string DataExtractionAbsolutePath(IWorkSpaceService ws, string hash)
            => ws.GetAbsolutePath(DataExtractionRelativePath(hash));

        public static string DataExtractionAbsolutePathFromRelative(IWorkSpaceService ws, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));

            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return ws.GetAbsolutePath(normalized);
        }
    }
}
