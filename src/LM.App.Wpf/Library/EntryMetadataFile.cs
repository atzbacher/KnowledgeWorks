using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    internal static class EntryMetadataFile
    {
        private static readonly JsonSerializerOptions s_json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static bool TryEnsureExists(Entry entry, string metadataPath)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(metadataPath))
                throw new ArgumentException("Metadata path must not be empty.", nameof(metadataPath));

            if (File.Exists(metadataPath))
                return true;

            var directory = Path.GetDirectoryName(metadataPath);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            var tmpPath = metadataPath + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);

                using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, entry, s_json);
                    stream.Flush();
                }

                if (File.Exists(metadataPath))
                    File.Delete(metadataPath);

                File.Move(tmpPath, metadataPath);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EntryMetadataFile] Failed to recreate metadata at '{metadataPath}': {ex}");
                try { File.Delete(tmpPath); } catch { /* ignore cleanup errors */ }
                return false;
            }
        }
    }
}
