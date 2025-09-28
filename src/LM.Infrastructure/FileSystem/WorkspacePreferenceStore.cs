using System;
using System.IO;
using System.Text.Json;

namespace LM.Infrastructure.FileSystem
{
    /// <summary>
    /// Persists workspace preferences for the current user.
    /// </summary>
    public sealed class WorkspacePreferenceStore
    {
        private readonly string _settingsPath;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public WorkspacePreferenceStore(string? settingsFilePath = null)
        {
            if (!string.IsNullOrWhiteSpace(settingsFilePath))
            {
                _settingsPath = Path.GetFullPath(settingsFilePath);
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            else
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var root = Path.Combine(baseDir, "KnowledgeWorks");
                Directory.CreateDirectory(root);
                _settingsPath = Path.Combine(root, "preferences.json");
            }
        }

        public string? TryGetLastWorkspacePath()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return null;
                }

                var json = File.ReadAllText(_settingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var payload = JsonSerializer.Deserialize<WorkspacePreferences>(json, JsonOptions);
                var path = payload?.LastWorkspacePath?.Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        public void SetLastWorkspacePath(string workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                throw new ArgumentException("Workspace path must not be empty.", nameof(workspacePath));
            }

            var preferences = new WorkspacePreferences
            {
                LastWorkspacePath = Path.GetFullPath(workspacePath)
            };

            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(preferences, JsonOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private sealed class WorkspacePreferences
        {
            public string? LastWorkspacePath { get; set; }
        }
    }
}
