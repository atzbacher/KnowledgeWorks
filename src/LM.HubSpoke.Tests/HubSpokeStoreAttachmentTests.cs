using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.Entries;
using LM.HubSpoke.FileSystem;
using LM.HubSpoke.Hubs;
using LM.HubSpoke.Models;
using LM.HubSpoke.Spokes;
using Xunit;

namespace LM.HubSpoke.Tests
{
    public sealed class HubSpokeStoreAttachmentTests
    {
        [Fact]
        public async Task GetByIdAsyncLoadsAttachmentsFromHook()
        {
            using var temp = new TempWorkspace();
            var workspace = new TestWorkspaceService(temp.RootPath);

            var entryId = "entry-attach";
            var hooksDir = Path.Combine(temp.RootPath, "entries", entryId, "hooks");
            Directory.CreateDirectory(hooksDir);

            var hub = new EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Attachment Entry",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                CreatedBy = new PersonRef("user", "User") { TimestampUtc = DateTime.UtcNow },
                UpdatedBy = new PersonRef("user", "User") { TimestampUtc = DateTime.UtcNow },
                Hooks = new EntryHooks { Article = "hooks/article.json" }
            };

            await HubJsonStore.SaveAsync(workspace, hub, CancellationToken.None);

            var articleHook = new ArticleHook
            {
                Article = new ArticleDetails { Title = "Attachment Entry" },
                Journal = new JournalInfo { Title = "Journal", Issue = new JournalIssue() },
                Identifier = new ArticleIdentifier()
            };

            await File.WriteAllTextAsync(
                Path.Combine(hooksDir, "article.json"),
                JsonSerializer.Serialize(articleHook, JsonStd.Options));

            var attachmentHook = new AttachmentHook
            {
                Attachments = new List<AttachmentHookItem>
                {
                    new AttachmentHookItem
                    {
                        AttachmentId = "att-1",
                        Title = string.Empty,
                        LibraryPath = Path.Combine("attachments", entryId, "supplement.pdf").Replace('\\', '/'),
                        Notes = "Important",
                        Tags = new List<string> { "analysis" },
                        Purpose = AttachmentKind.Supplement,
                        AddedBy = "tester",
                        AddedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)
                    }
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(hooksDir, "attachments.json"),
                JsonSerializer.Serialize(attachmentHook, JsonStd.Options));

            var store = new HubSpokeStore(
                workspace,
                new NoopHasher(),
                new ISpokeHandler[]
                {
                    new ArticleSpokeHandler(),
                    new DocumentSpokeHandler(),
                    new LitSearchSpokeHandler(workspace)
                });

            var entry = await store.GetByIdAsync(entryId);

            Assert.NotNull(entry);
            Assert.Single(entry!.Attachments);
            var attachment = entry.Attachments[0];
            Assert.Equal("att-1", attachment.Id);
            Assert.Equal("supplement.pdf", attachment.Title);
            Assert.Equal(Path.Combine("attachments", entryId, "supplement.pdf").Replace('\\', '/'), attachment.RelativePath);
            Assert.Equal("Important", attachment.Notes);
            Assert.Equal(AttachmentKind.Supplement, attachment.Kind);
            Assert.Equal("tester", attachment.AddedBy);
            Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), attachment.AddedUtc);
            Assert.Contains("analysis", attachment.Tags);
        }

        [Fact]
        public async Task GetByIdAsyncUsesArticleAssetsWhenAttachmentHookMissing()
        {
            using var temp = new TempWorkspace();
            var workspace = new TestWorkspaceService(temp.RootPath);

            var entryId = "entry-legacy-article";
            var hooksDir = Path.Combine(temp.RootPath, "entries", entryId, "hooks");
            Directory.CreateDirectory(hooksDir);

            var timestamp = new DateTime(2023, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            var hub = new EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Legacy Article",
                CreatedUtc = timestamp,
                UpdatedUtc = timestamp,
                CreatedBy = new PersonRef("legacy", "Legacy User") { TimestampUtc = timestamp },
                UpdatedBy = new PersonRef("legacy", "Legacy User") { TimestampUtc = timestamp },
                Hooks = new EntryHooks { Article = "hooks/article.json" }
            };

            await HubJsonStore.SaveAsync(workspace, hub, CancellationToken.None);

            var manuscriptPath = Path.Combine("library", "legacy", "main.pdf").Replace('\\', '/');
            var supplementPath = Path.Combine("library", "legacy", "supplement.pdf").Replace('\\', '/');

            var articleHook = new ArticleHook
            {
                Article = new ArticleDetails { Title = "Legacy Article" },
                Journal = new JournalInfo { Title = "Journal", Issue = new JournalIssue() },
                Identifier = new ArticleIdentifier(),
                Assets = new List<ArticleAsset>
                {
                    new ArticleAsset
                    {
                        Title = "main.pdf",
                        OriginalFilename = "main.pdf",
                        StoragePath = manuscriptPath,
                        Hash = "sha256-" + new string('a', 64),
                        Purpose = ArticleAssetPurpose.Manuscript
                    },
                    new ArticleAsset
                    {
                        Title = "supplement.pdf",
                        OriginalFilename = "supplement.pdf",
                        StoragePath = supplementPath,
                        Hash = "sha256-" + new string('b', 64),
                        Purpose = ArticleAssetPurpose.Supplement,
                        OriginalFolderPath = @"C:\legacy\source"
                    }
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(hooksDir, "article.json"),
                JsonSerializer.Serialize(articleHook, JsonStd.Options));

            var store = new HubSpokeStore(
                workspace,
                new NoopHasher(),
                new ISpokeHandler[]
                {
                    new ArticleSpokeHandler(),
                    new DocumentSpokeHandler(),
                    new LitSearchSpokeHandler(workspace)
                });

            var entry = await store.GetByIdAsync(entryId);

            Assert.NotNull(entry);
            Assert.Single(entry!.Attachments);
            var attachment = entry.Attachments[0];
            Assert.Equal("supplement.pdf", attachment.Title);
            Assert.Equal(supplementPath, attachment.RelativePath);
            Assert.Equal(AttachmentKind.Supplement, attachment.Kind);
            Assert.Equal("Legacy User", attachment.AddedBy);
            Assert.Equal(timestamp, attachment.AddedUtc);
            Assert.Contains(@"C:\legacy\source", entry.Links);
        }

        [Fact]
        public async Task GetByIdAsyncUsesDocumentAssetsWhenAttachmentHookMissing()
        {
            using var temp = new TempWorkspace();
            var workspace = new TestWorkspaceService(temp.RootPath);

            var entryId = "entry-legacy-doc";
            var hooksDir = Path.Combine(temp.RootPath, "entries", entryId, "hooks");
            Directory.CreateDirectory(hooksDir);

            var timestamp = new DateTime(2022, 5, 2, 9, 0, 0, DateTimeKind.Utc);

            var hub = new EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Legacy Document",
                CreatedUtc = timestamp,
                UpdatedUtc = timestamp,
                CreatedBy = new PersonRef("doc", "Doc Owner") { TimestampUtc = timestamp },
                UpdatedBy = new PersonRef("doc", "Doc Owner") { TimestampUtc = timestamp },
                Hooks = new EntryHooks { Document = "hooks/document.json" }
            };

            await HubJsonStore.SaveAsync(workspace, hub, CancellationToken.None);

            var primaryPath = Path.Combine("library", "docs", "main.pdf").Replace('\\', '/');
            var supplementPath = Path.Combine("library", "docs", "appendix.pdf").Replace('\\', '/');

            var documentHook = new DocumentHook
            {
                Title = "Legacy Document",
                Assets = new List<AssetRef>
                {
                    new AssetRef
                    {
                        Role = "primary_pdf",
                        StoragePath = primaryPath,
                        Hash = "sha256-" + new string('c', 64),
                        OriginalFilename = "main.pdf"
                    },
                    new AssetRef
                    {
                        Role = "supplement",
                        StoragePath = supplementPath,
                        Hash = "sha256-" + new string('d', 64),
                        OriginalFilename = "appendix.pdf"
                    }
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(hooksDir, "document.json"),
                JsonSerializer.Serialize(documentHook, JsonStd.Options));

            var store = new HubSpokeStore(
                workspace,
                new NoopHasher(),
                new ISpokeHandler[]
                {
                    new ArticleSpokeHandler(),
                    new DocumentSpokeHandler(),
                    new LitSearchSpokeHandler(workspace)
                });

            var entry = await store.GetByIdAsync(entryId);

            Assert.NotNull(entry);
            Assert.Single(entry!.Attachments);
            var attachment = entry.Attachments[0];
            Assert.Equal("appendix.pdf", attachment.Title);
            Assert.Equal(supplementPath, attachment.RelativePath);
            Assert.Equal(AttachmentKind.Supplement, attachment.Kind);
            Assert.Equal("Doc Owner", attachment.AddedBy);
            Assert.Equal(timestamp, attachment.AddedUtc);
        }

        private sealed class NoopHasher : IHasher
        {
            public Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
                => Task.FromResult("hash");
        }

        private sealed class TestWorkspaceService : IWorkSpaceService
        {
            public TestWorkspaceService(string root)
            {
                WorkspacePath = root;
                Directory.CreateDirectory(root);
            }

            public string? WorkspacePath { get; private set; }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                WorkspacePath = absoluteWorkspacePath;
                Directory.CreateDirectory(absoluteWorkspacePath);
                return Task.CompletedTask;
            }

            public string GetAbsolutePath(string relativePath)
            {
                relativePath ??= string.Empty;
                return Path.Combine(WorkspacePath ?? string.Empty, relativePath);
            }

            public string GetLocalDbPath() => Path.Combine(WorkspacePath ?? string.Empty, "metadata.db");

            public string GetWorkspaceRoot() => WorkspacePath ?? throw new InvalidOperationException("WorkspacePath not set");
        }

        private sealed class TempWorkspace : IDisposable
        {
            public TempWorkspace()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "kw-hub-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public string RootPath { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootPath))
                        Directory.Delete(RootPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }
    }
}
