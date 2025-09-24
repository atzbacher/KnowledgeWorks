#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;

namespace LM.App.Wpf.Diagnostics
{
    /// <summary>
    /// Writes a JSON snapshot of staged items for debugging.
    /// </summary>
    internal static class StagingDebugDumper
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true
        };

        internal static void TryDump(IWorkSpaceService workspace, StagingItem item)
        {
            if (!DebugFlags.DumpStagingJson) return;
            if (workspace is null || item is null) return;

            try
            {
                var root = workspace.GetWorkspaceRoot();
                var targetDir = Path.Combine(root, "_debug", "staging");
                Directory.CreateDirectory(targetDir);

                var safeName = MakeSafeFileName(item.OriginalFileName ?? item.FilePath ?? "staged");
                var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{safeName}.json";
                var absPath = Path.Combine(targetDir, fileName);

                var spokes = item.ArticleHook is null
                    ? Array.Empty<string>()
                    : new[] { "hooks/article.json" }; // mirrors ArticleHookComposer persistence

                var payload = new
                {
                    Utc = DateTime.UtcNow,
                    SourceFile = item.FilePath,
                    Spokes = spokes,
                    Staging = item,
                    ArticleHook = item.ArticleHook
                };

                File.WriteAllText(absPath, JsonSerializer.Serialize(payload, s_jsonOptions));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[StagingDebugDumper] Failed to write dump: {ex}");
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 120 ? name[..120] : name;
        }
    }
}
