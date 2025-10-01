#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Writes hook JSON files under entries/&lt;id&gt;/hooks/.
    /// Infrastructure concern: I/O only. Callers provide the hook object.
    /// </summary>
    internal sealed class HookWriter
    {
        private const int RetryDelayMilliseconds = 200;
        private static readonly TimeSpan s_lockTimeout = TimeSpan.FromMinutes(5);

        private readonly IWorkSpaceService _workspace;

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true
        };

        public HookWriter(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        /// <summary>
        /// Persist the ArticleHook for a given entry id.
        /// Creates entries/&lt;entryId&gt;/hooks/article.json (directories included).
        /// </summary>
        public async Task SaveArticleAsync(string entryId, HookM.ArticleHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));

            var relDir = Path.Combine("entries", entryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "article.json");

            await WriteJsonAsync(absPath, hook, ct).ConfigureAwait(false);
        }

        public async Task SaveAttachmentsAsync(string entryId, HookM.AttachmentHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));

            var relDir = Path.Combine("entries", entryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "attachments.json");

            await WriteJsonAsync(absPath, hook, ct).ConfigureAwait(false);
        }

        public async Task SavePdfAnnotationsAsync(string entryId, string pdfHash, HookM.PdfAnnotationsHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (string.IsNullOrWhiteSpace(pdfHash))
                throw new ArgumentException("PDF hash must be non-empty.", nameof(pdfHash));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));

            var normalizedHash = pdfHash.Trim().ToLowerInvariant();
            var normalizedEntryId = entryId.Trim();
            if (normalizedEntryId.Length == 0)
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));

            var relDir = Path.Combine("entries", normalizedEntryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "pdf_annotations.json");

            await WriteJsonAsync(absPath, hook, ct).ConfigureAwait(false);

            var changeEvent = new HookM.EntryChangeLogEvent
            {
                Action = "pdf-annotations-updated",
                PerformedBy = Environment.UserName,
                TimestampUtc = DateTime.UtcNow
            };

            var changeLog = new HookM.EntryChangeLogHook
            {
                Events = new List<HookM.EntryChangeLogEvent> { changeEvent }
            };

            await AppendChangeLogAsync(normalizedEntryId, changeLog, ct).ConfigureAwait(false);

            CleanupLegacyPdfHook(normalizedHash, normalizedEntryId);
        }

        private void CleanupLegacyPdfHook(string normalizedHash, string normalizedEntryId)
        {
            if (string.IsNullOrWhiteSpace(normalizedHash))
            {
                return;
            }

            if (string.Equals(normalizedHash, normalizedEntryId, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var legacyDir = Path.Combine("entries", normalizedHash, "hooks");
                var legacyAbsolute = _workspace.GetAbsolutePath(legacyDir);
                if (string.IsNullOrWhiteSpace(legacyAbsolute) || !Directory.Exists(legacyAbsolute))
                {
                    return;
                }

                TryDeleteFile(Path.Combine(legacyAbsolute, "pdf_annotations.json"));
                TryDeleteFile(Path.Combine(legacyAbsolute, "changelog.json"));

                if (IsDirectoryEmpty(legacyAbsolute))
                {
                    Directory.Delete(legacyAbsolute, recursive: false);

                    var parent = Path.GetDirectoryName(legacyAbsolute);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent) && IsDirectoryEmpty(parent))
                    {
                        Directory.Delete(parent, recursive: false);
                    }
                }
            }
            catch
            {
                // Best-effort cleanup; ignore failures.
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        private static bool IsDirectoryEmpty(string path)
        {
            try
            {
                return Directory.GetFileSystemEntries(path).Length == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task AppendChangeLogAsync(string entryId, HookM.EntryChangeLogHook hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be non-empty.", nameof(entryId));
            if (hook is null)
                throw new ArgumentNullException(nameof(hook));
            if (hook.Events is null || hook.Events.Count == 0)
                return;

            var relDir = Path.Combine("entries", entryId, "hooks");
            var absDir = _workspace.GetAbsolutePath(relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, "changelog.json");

            await WithFileLockAsync(
                absPath,
                async (context, token) =>
                {
                    var existing = await ReadChangeLogAsync(context.Path, token).ConfigureAwait(false)
                                   ?? new HookM.EntryChangeLogHook();

                    existing.Events ??= new List<HookM.EntryChangeLogEvent>();

                    foreach (var evt in hook.Events)
                    {
                        if (evt is not null)
                        {
                            existing.Events.Add(evt);
                        }
                    }

                    await SerializeToTempAsync(context.TempPath, existing, token).ConfigureAwait(false);
                    await MoveTempIntoPlaceAsync(context.TempPath, context.Path, token).ConfigureAwait(false);
                },
                ct).ConfigureAwait(false);
        }

        private static Task WriteJsonAsync<T>(string path, T payload, CancellationToken ct)
            => WithFileLockAsync(
                path,
                async (context, token) =>
                {
                    await SerializeToTempAsync(context.TempPath, payload, token).ConfigureAwait(false);
                    await MoveTempIntoPlaceAsync(context.TempPath, context.Path, token).ConfigureAwait(false);
                },
                ct);

        private static async Task WithFileLockAsync(string path, Func<FileLockContext, CancellationToken, Task> callback, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(callback);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lockPath = path + ".lock";
            var tempPath = path + ".tmp";

            var lockHandle = AcquireLock(lockPath);
            try
            {
                await callback(new FileLockContext(path, tempPath), ct).ConfigureAwait(false);
            }
            finally
            {
                await lockHandle.DisposeAsync().ConfigureAwait(false);

                try
                {
                    File.Delete(lockPath);
                }
                catch
                {
                    // Ignore lock cleanup issues.
                }

                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { /* best effort cleanup */ }
                }
            }
        }

        private static async Task<HookM.EntryChangeLogHook?> ReadChangeLogAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                await using var readStream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096,
                    useAsync: true);

                return await JsonSerializer.DeserializeAsync<HookM.EntryChangeLogHook>(readStream, s_jsonOptions, ct).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        private static async Task SerializeToTempAsync<T>(string tempPath, T payload, CancellationToken ct)
        {
            await using var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await JsonSerializer.SerializeAsync(stream, payload, s_jsonOptions, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        private static async Task MoveTempIntoPlaceAsync(string tempPath, string destinationPath, CancellationToken ct)
        {
            var waitStart = DateTime.UtcNow;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    File.Move(tempPath, destinationPath, overwrite: true);
                    return;
                }
                catch (IOException) when (ShouldRetry(waitStart))
                {
                    await Task.Delay(RetryDelayMilliseconds, ct).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException) when (ShouldRetry(waitStart))
                {
                    await Task.Delay(RetryDelayMilliseconds, ct).ConfigureAwait(false);
                }
            }
        }

        private static bool ShouldRetry(DateTime waitStart)
            => DateTime.UtcNow - waitStart < s_lockTimeout;

        private static FileStream AcquireLock(string lockPath)
        {
            var waitStart = DateTime.UtcNow;

            while (true)
            {
                try
                {
                    var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    stream.SetLength(0);
                    return stream;
                }
                catch (IOException) when (File.Exists(lockPath))
                {
                    if (ShouldBreakLock(lockPath, waitStart))
                    {
                        continue;
                    }

                    Thread.Sleep(RetryDelayMilliseconds);
                }
                catch (UnauthorizedAccessException) when (File.Exists(lockPath))
                {
                    if (ShouldBreakLock(lockPath, waitStart))
                    {
                        continue;
                    }

                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }
        }

        private static bool ShouldBreakLock(string lockPath, DateTime waitStart)
        {
            if (DateTime.UtcNow - waitStart >= s_lockTimeout)
            {
                throw new IOException($"Timed out acquiring lock for '{lockPath}'.");
            }

            var lastWrite = File.GetLastWriteTimeUtc(lockPath);
            if (lastWrite == DateTime.MinValue || DateTime.UtcNow - lastWrite > s_lockTimeout)
            {
                try
                {
                    File.Delete(lockPath);
                    return true;
                }
                catch (IOException)
                {
                    // Someone else still holds the lock.
                }
                catch (UnauthorizedAccessException)
                {
                    // Someone else still holds the lock.
                }
            }

            return false;
        }

        private readonly record struct FileLockContext(string Path, string TempPath);
    }
}
