using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.HubSpoke.Models;
using LM.Infrastructure.Hooks;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class EntryEditorViewModelTests
    {
        [Fact]
        public async Task SaveAsync_WithMetadataChanges_WritesChangeLog()
        {
            using var workspace = new TestWorkspace();
            var entry = new Entry
            {
                Id = "entry-1",
                Title = "Original Title",
                DisplayName = "Original Display",
                Source = "Journal",
                Year = 2020,
                Authors = new List<string> { "Doe, John" },
                Tags = new List<string> { "old" },
                MainFilePath = "library\\entry.pdf"
            };

            var store = new RecordingEntryStore(entry);
            var orchestrator = new HookOrchestrator(workspace);
            var viewModel = new EntryEditorViewModel(store, orchestrator, workspace);

            var loaded = await viewModel.LoadAsync(entry.Id).ConfigureAwait(true);
            Assert.True(loaded);
            Assert.NotNull(viewModel.Item);

            viewModel.Item!.Title = "Updated Title";
            viewModel.Item.DisplayName = "Updated Display";
            viewModel.Item.TagsCsv = "updated";

            await InvokeSaveAsync(viewModel).ConfigureAwait(true);

            var changelogPath = Path.Combine(workspace.WorkspacePath!, "entries", entry.Id, "hooks", "changelog.json");
            Assert.True(File.Exists(changelogPath));

            var hook = JsonSerializer.Deserialize<EntryChangeLogHook>(await File.ReadAllTextAsync(changelogPath).ConfigureAwait(true));
            Assert.NotNull(hook);
            var evt = Assert.Single(hook!.Events);
            var expectedUser = string.IsNullOrWhiteSpace(Environment.UserName) ? "unknown" : Environment.UserName;
            Assert.Equal("EntryUpdated", evt.Action);
            Assert.Equal(expectedUser, evt.PerformedBy);
            Assert.NotNull(evt.Details);
            Assert.Equal(AttachmentKind.Metadata, evt.Details!.Purpose);
            Assert.Equal(entry.Id, evt.Details.AttachmentId);
            Assert.Equal("Updated Display", evt.Details.Title);
            Assert.Equal("library/entry.pdf", evt.Details.LibraryPath);
            Assert.Contains("changed:title", evt.Details.Tags);
            Assert.Contains("changed:displayName", evt.Details.Tags);
            Assert.Contains("changed:tags", evt.Details.Tags);
        }

        [Fact]
        public async Task SaveAsync_WithNoChanges_DoesNotCreateChangeLog()
        {
            using var workspace = new TestWorkspace();
            var entry = new Entry
            {
                Id = "entry-2",
                Title = "Original Title",
                DisplayName = "Original Display",
                MainFilePath = "library\\entry.pdf"
            };

            var store = new RecordingEntryStore(entry);
            var orchestrator = new HookOrchestrator(workspace);
            var viewModel = new EntryEditorViewModel(store, orchestrator, workspace);

            var loaded = await viewModel.LoadAsync(entry.Id).ConfigureAwait(true);
            Assert.True(loaded);

            await InvokeSaveAsync(viewModel).ConfigureAwait(true);

            var changelogPath = Path.Combine(workspace.WorkspacePath!, "entries", entry.Id, "hooks", "changelog.json");
            Assert.False(File.Exists(changelogPath));
        }

        private static Task InvokeSaveAsync(EntryEditorViewModel viewModel)
        {
            var method = typeof(EntryEditorViewModel).GetMethod("SaveAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = method!.Invoke(viewModel, Array.Empty<object>());
            return result is Task task ? task : Task.CompletedTask;
        }

        private sealed class RecordingEntryStore : IEntryStore
        {
            private readonly Entry _entry;

            public RecordingEntryStore(Entry entry)
            {
                _entry = Clone(entry);
            }

            public Entry? SavedEntry { get; private set; }

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default)
            {
                SavedEntry = Clone(entry);
                return Task.CompletedTask;
            }

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
                => Task.FromResult(id == _entry.Id ? Clone(_entry) : null);

            public IAsyncEnumerable<Entry> EnumerateAsync(CancellationToken ct = default)
            {
                return Empty();

                static async IAsyncEnumerable<Entry> Empty()
                {
                    await Task.CompletedTask;
                    yield break;
                }
            }

            public Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);

            public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);

            private static Entry Clone(Entry source)
            {
                return new Entry
                {
                    Id = source.Id,
                    Title = source.Title,
                    DisplayName = source.DisplayName,
                    Source = source.Source,
                    Year = source.Year,
                    Authors = source.Authors?.ToList() ?? new List<string>(),
                    Tags = source.Tags?.ToList() ?? new List<string>(),
                    MainFilePath = source.MainFilePath,
                    OriginalFileName = source.OriginalFileName,
                    AddedBy = source.AddedBy,
                    AddedOnUtc = source.AddedOnUtc,
                    IsInternal = source.IsInternal,
                    Doi = source.Doi,
                    Pmid = source.Pmid,
                    InternalId = source.InternalId,
                    UserNotes = source.UserNotes
                };
            }
        }

        private sealed class TestWorkspace : IWorkSpaceService, IDisposable
        {
            public string Root { get; } = Path.Combine(Path.GetTempPath(), "kw-entry-editor-tests-" + Guid.NewGuid().ToString("N"));

            public TestWorkspace()
            {
                Directory.CreateDirectory(Root);
            }

            public string? WorkspacePath => Root;

            public string GetWorkspaceRoot() => Root;

            public string GetLocalDbPath() => Path.Combine(Root, "local.db");

            public string GetAbsolutePath(string relativePath)
            {
                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(Root, normalized);
            }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                Directory.CreateDirectory(absoluteWorkspacePath);
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
