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
        public static string StorageRoot(IWorkSpaceService ws) => Path.Combine(ws.GetWorkspaceRoot(), "storage");
        public static string ExtractionRoot(IWorkSpaceService ws) => Path.Combine(ws.GetWorkspaceRoot(), "extraction");

        public static string EntryDir(IWorkSpaceService ws, string id) => Path.Combine(EntriesRoot(ws), id);

        public static string HubPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hub.json");
        public static string ArticleHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "article.json");
        public static string DocumentHookPath(IWorkSpaceService ws, string id) => Path.Combine(EntryDir(ws, id), "hooks", "document.json");
    }
}
