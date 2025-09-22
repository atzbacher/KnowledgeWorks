#nullable enable
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

        public static string ExtractionDescriptorPath(IWorkSpaceService ws, string regionHash)
        {
            var bucket = ExtractionBucket(ws, regionHash);
            return Path.Combine(bucket, $"{NormalizeHash(regionHash)}.json");
        }

        public static string ExtractionBucket(IWorkSpaceService ws, string regionHash)
        {
            var hash = NormalizeHash(regionHash);
            var first = Segment(hash, 0);
            var second = Segment(hash, 2);
            return Path.Combine(ExtractionRoot(ws), first, second);
        }

        public static string EntryDir(IWorkSpaceService ws, string id) => Path.Combine(EntriesRoot(ws), id);

        public static string HubPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hub.json");
        public static string ArticleHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "article.json");
        public static string DocumentHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "document.json");
        public static string LitSearchHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "litsearch.json");
        public static string NotesHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "notes.json");

        private static string NormalizeHash(string regionHash)
            => (regionHash ?? string.Empty).Trim().ToLowerInvariant();

        private static string Segment(string hash, int start)
        {
            if (hash.Length <= start)
                return "00";

            var length = System.Math.Min(2, hash.Length - start);
            var segment = hash.Substring(start, length);
            return segment.PadRight(2, '0');
        }
    }
}
