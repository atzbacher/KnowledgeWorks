using System;
using System.IO;
using LM.Infrastructure.FileSystem;
using Xunit;

namespace LM.Infrastructure.Tests
{
    public sealed class WorkspacePreferenceStoreTests
    {
        [Fact]
        public void TryGetLastWorkspacePath_ReturnsNull_WhenFileMissing()
        {
            RunWithSettingsPath(settingsPath =>
            {
                var store = new WorkspacePreferenceStore(settingsPath);

                var result = store.TryGetLastWorkspacePath();

                Assert.Null(result);
            });
        }

        [Fact]
        public void SetLastWorkspacePath_PersistsValue()
        {
            RunWithSettingsPath(settingsPath =>
            {
                var workspaceRoot = Path.Combine(Path.GetTempPath(), "kw_pref_workspace_" + Guid.NewGuid().ToString("N"));

                var store = new WorkspacePreferenceStore(settingsPath);
                store.SetLastWorkspacePath(workspaceRoot);

                var reloaded = new WorkspacePreferenceStore(settingsPath);
                var result = reloaded.TryGetLastWorkspacePath();

                Assert.Equal(Path.GetFullPath(workspaceRoot), result);
            });
        }

        [Fact]
        public void TryGetLastWorkspacePath_ReturnsNull_WhenJsonInvalid()
        {
            RunWithSettingsPath(settingsPath =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                File.WriteAllText(settingsPath, "invalid");

                var store = new WorkspacePreferenceStore(settingsPath);
                var result = store.TryGetLastWorkspacePath();

                Assert.Null(result);
            });
        }

        private static void RunWithSettingsPath(Action<string> test)
        {
            var directory = Path.Combine(Path.GetTempPath(), "kw_pref_store_" + Guid.NewGuid().ToString("N"));
            var settingsPath = Path.Combine(directory, "settings.json");

            try
            {
                test(settingsPath);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
