#nullable enable
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.FileSystem;
using LM.HubSpoke.Models;

namespace LM.HubSpoke.Hubs
{
    internal static class HubJsonStore
    {
        public static async Task SaveAsync(IWorkSpaceService ws,EntryHub hub, CancellationToken ct)
        {
            var dir = WorkspaceLayout.EntryDir(ws, hub.EntryId);
            Directory.CreateDirectory(Path.Combine(dir, "hooks"));
            var path = WorkspaceLayout.HubPath(ws, hub.EntryId);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(hub,JsonStd.Options), ct);
        }

        public static async Task<EntryHub?> LoadAsync(IWorkSpaceService ws, string id, CancellationToken ct)
        {
            var path = WorkspaceLayout.HubPath(ws, id);
            if (!File.Exists(path)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(path, ct);
                return JsonSerializer.Deserialize<EntryHub>(json, JsonStd.Options);
            }
            catch { return null; }
        }
    }
}
