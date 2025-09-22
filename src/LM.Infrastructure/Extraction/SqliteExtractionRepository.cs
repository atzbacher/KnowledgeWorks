using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Extraction;
using LM.HubSpoke.FileSystem;
using Microsoft.Data.Sqlite;

namespace LM.Infrastructure.Extraction
{
    public sealed class SqliteExtractionRepository : IExtractionRepository
    {
        private const int SessionRetentionLimit = 200;

        private readonly IWorkSpaceService _workspace;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private string? _dbPath;
        private volatile bool _initialized;

        public SqliteExtractionRepository(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task UpsertAsync(RegionDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(descriptor.RegionHash))
                throw new ArgumentException("Region hash must not be empty.", nameof(descriptor));

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
INSERT INTO region_descriptor(
    region_hash, entry_hub_id, source_rel_path, source_sha256,
    page_number, bounds, ocr_text, tags, notes, annotation,
    created_utc, updated_utc, last_export_status, last_error_message,
    office_package_path, image_path, ocr_text_path, exporter_id, extra_metadata)
VALUES(
    $hash, $entry, $source, $sourceSha,
    $page, $bounds, $ocr, $tags, $notes, $annotation,
    $created, $updated, $status, $error,
    $office, $image, $ocrPath, $exporter, $metadata)
ON CONFLICT(region_hash) DO UPDATE SET
    entry_hub_id = excluded.entry_hub_id,
    source_rel_path = excluded.source_rel_path,
    source_sha256 = excluded.source_sha256,
    page_number = excluded.page_number,
    bounds = excluded.bounds,
    ocr_text = excluded.ocr_text,
    tags = excluded.tags,
    notes = excluded.notes,
    annotation = excluded.annotation,
    created_utc = excluded.created_utc,
    updated_utc = excluded.updated_utc,
    last_export_status = excluded.last_export_status,
    last_error_message = excluded.last_error_message,
    office_package_path = excluded.office_package_path,
    image_path = excluded.image_path,
    ocr_text_path = excluded.ocr_text_path,
    exporter_id = excluded.exporter_id,
    extra_metadata = excluded.extra_metadata;";

                RegionDescriptorMapper.BindDescriptor(cmd, descriptor);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await UpsertFtsAsync(connection, transaction, descriptor, cancellationToken);
            await UpsertRecentSessionAsync(
                connection,
                transaction,
                descriptor.RegionHash,
                descriptor.UpdatedUtc ?? descriptor.CreatedUtc,
                durationMs: null,
                wasCached: null,
                outputsJson: null,
                cancellationToken);
            await TrimRecentSessionsAsync(connection, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            await PersistDescriptorAsync(descriptor, cancellationToken);
        }

        public async Task<RegionDescriptor?> GetAsync(string regionHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(regionHash)) return null;
            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM region_descriptor WHERE region_hash=$hash LIMIT 1;";
            command.Parameters.AddWithValue("$hash", regionHash);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                return RegionDescriptorMapper.ReadDescriptor(reader);

            return null;
        }

        public async IAsyncEnumerable<RegionDescriptor> ListByEntryAsync(
            string entryHubId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entryHubId)) yield break;

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT *
FROM region_descriptor
WHERE entry_hub_id = $entry
ORDER BY COALESCE(updated_utc, created_utc) DESC;";
            command.Parameters.AddWithValue("$entry", entryHubId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return RegionDescriptorMapper.ReadDescriptor(reader);
            }
        }

        public async Task<IReadOnlyList<RegionDescriptor>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        {
            if (take <= 0) return Array.Empty<RegionDescriptor>();

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var results = new List<RegionDescriptor>();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT d.*
FROM (
    SELECT region_hash, MAX(completed_utc) AS completed_utc
    FROM region_recent_session
    GROUP BY region_hash
    ORDER BY completed_utc DESC
    LIMIT $take
) latest
JOIN region_descriptor d ON d.region_hash = latest.region_hash
ORDER BY latest.completed_utc DESC;";
                command.Parameters.AddWithValue("$take", take);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    results.Add(RegionDescriptorMapper.ReadDescriptor(reader));
            }

            if (results.Count == 0)
            {
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = @"
SELECT *
FROM region_descriptor
ORDER BY COALESCE(updated_utc, created_utc) DESC
LIMIT $take;";
                fallback.Parameters.AddWithValue("$take", take);

                await using var reader = await fallback.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    results.Add(RegionDescriptorMapper.ReadDescriptor(reader));
            }

            return results;
        }

        public async Task<IReadOnlyList<RegionDescriptor>> SearchAsync(string query, int take, CancellationToken cancellationToken = default)
        {
            if (take <= 0) return Array.Empty<RegionDescriptor>();
            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var expression = BuildMatchExpression(query);
            var descriptors = new List<RegionDescriptor>();

            if (!string.IsNullOrEmpty(expression))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT d.*
FROM region_descriptor_fts f
JOIN region_descriptor d ON d.region_hash = f.region_hash
WHERE region_descriptor_fts MATCH $expr
ORDER BY COALESCE(d.updated_utc, d.created_utc) DESC
LIMIT $take;";
                command.Parameters.AddWithValue("$expr", expression);
                command.Parameters.AddWithValue("$take", take);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    descriptors.Add(RegionDescriptorMapper.ReadDescriptor(reader));

                return descriptors;
            }

            await using var fallback = connection.CreateCommand();
            fallback.CommandText = @"
SELECT *
FROM region_descriptor
ORDER BY COALESCE(updated_utc, created_utc) DESC
LIMIT $take;";
            fallback.Parameters.AddWithValue("$take", take);

            await using var rdr = await fallback.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
                descriptors.Add(RegionDescriptorMapper.ReadDescriptor(rdr));

            return descriptors;
        }

        public async Task UpdateStatusAsync(
            string regionHash,
            RegionExportStatus status,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(regionHash))
                throw new ArgumentException("Region hash must not be empty.", nameof(regionHash));

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE region_descriptor
SET last_export_status = $status,
    last_error_message = $error,
    updated_utc = $updated
WHERE region_hash = $hash;";
                command.Parameters.AddWithValue("$status", status.ToString());
                command.Parameters.AddWithValue("$error", (object?)errorMessage ?? DBNull.Value);
                command.Parameters.AddWithValue("$updated", RegionDescriptorMapper.FormatDateTime(DateTime.UtcNow));
                command.Parameters.AddWithValue("$hash", regionHash);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        public async Task DeleteAsync(string regionHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(regionHash)) return;

            await EnsureInitializedAsync(cancellationToken);

            RegionDescriptor? descriptor = null;

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var fetch = connection.CreateCommand())
            {
                fetch.Transaction = transaction;
                fetch.CommandText = "SELECT * FROM region_descriptor WHERE region_hash=$hash LIMIT 1;";
                fetch.Parameters.AddWithValue("$hash", regionHash);
                await using var reader = await fetch.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                    descriptor = RegionDescriptorMapper.ReadDescriptor(reader);
            }

            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM region_descriptor WHERE region_hash=$hash;";
                delete.Parameters.AddWithValue("$hash", regionHash);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var fts = connection.CreateCommand())
            {
                fts.Transaction = transaction;
                fts.CommandText = "DELETE FROM region_descriptor_fts WHERE region_hash=$hash;";
                fts.Parameters.AddWithValue("$hash", regionHash);
                await fts.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var sessions = connection.CreateCommand())
            {
                sessions.Transaction = transaction;
                sessions.CommandText = "DELETE FROM region_recent_session WHERE region_hash=$hash;";
                sessions.Parameters.AddWithValue("$hash", regionHash);
                await sessions.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            if (descriptor is not null)
            {
                DeleteDescriptorArtifacts(descriptor);
            }
        }

        public async Task SaveSessionAsync(RegionExportResult result, CancellationToken cancellationToken = default)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));
            if (result.Descriptor is null)
                throw new ArgumentException("Result descriptor must be provided.", nameof(result));

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var completed = result.CompletedUtc == default ? DateTime.UtcNow : result.CompletedUtc;
            var durationMs = (long)Math.Max(0, Math.Round(result.Duration.TotalMilliseconds));
            var outputsJson = RegionDescriptorMapper.SerializeAdditionalOutputs(result.AdditionalOutputs);

            await UpsertRecentSessionAsync(
                connection,
                transaction,
                result.Descriptor.RegionHash,
                completed,
                durationMs,
                result.WasCached,
                outputsJson,
                cancellationToken);

            await TrimRecentSessionsAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<RegionExportResult>> GetRecentSessionsAsync(int take, CancellationToken cancellationToken = default)
        {
            if (take <= 0) return Array.Empty<RegionExportResult>();

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT d.*, 
       s.completed_utc AS session_completed_utc,
       s.duration_ms AS session_duration_ms,
       s.was_cached AS session_was_cached,
       s.additional_outputs AS session_additional_outputs
FROM region_recent_session s
JOIN region_descriptor d ON d.region_hash = s.region_hash
ORDER BY s.completed_utc DESC, s.session_id DESC
LIMIT $take;";
            command.Parameters.AddWithValue("$take", take);

            var sessions = new List<RegionExportResult>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                sessions.Add(RegionDescriptorMapper.ReadExportResult(reader));

            return sessions;
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized) return;

                var dbPath = _workspace.GetLocalDbPath();
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await ExtractionSchemaBootstrapper.EnsureAsync(_workspace, cancellationToken);

                _dbPath = dbPath;
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private SqliteConnection CreateConnection()
        {
            if (string.IsNullOrWhiteSpace(_dbPath))
                throw new InvalidOperationException("Repository has not been initialized.");

            return new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared;");
        }

        private static async Task UpsertFtsAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            RegionDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM region_descriptor_fts WHERE region_hash=$hash;";
                delete.Parameters.AddWithValue("$hash", descriptor.RegionHash);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            var joinedTags = descriptor.Tags.Count == 0
                ? string.Empty
                : string.Join(' ', descriptor.Tags.Select(t => t.ToLowerInvariant()));

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO region_descriptor_fts(
    region_hash, entry_hub_id, source_rel_path, ocr_text, notes, annotation, tags)
VALUES($hash, $entry, $source, $ocr, $notes, $annotation, $tags);";
            insert.Parameters.AddWithValue("$hash", descriptor.RegionHash);
            insert.Parameters.AddWithValue("$entry", descriptor.EntryHubId);
            insert.Parameters.AddWithValue("$source", descriptor.SourceRelativePath);
            insert.Parameters.AddWithValue("$ocr", descriptor.OcrText ?? string.Empty);
            insert.Parameters.AddWithValue("$notes", descriptor.Notes ?? string.Empty);
            insert.Parameters.AddWithValue("$annotation", descriptor.Annotation ?? string.Empty);
            insert.Parameters.AddWithValue("$tags", joinedTags);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task UpsertRecentSessionAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string regionHash,
            DateTime completedUtc,
            long? durationMs,
            bool? wasCached,
            string? outputsJson,
            CancellationToken cancellationToken)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
INSERT INTO region_recent_session(region_hash, completed_utc, duration_ms, was_cached, additional_outputs)
VALUES($hash, $completed, $duration, $cached, $outputs)
ON CONFLICT(region_hash, completed_utc) DO UPDATE SET
    duration_ms = COALESCE(excluded.duration_ms, region_recent_session.duration_ms),
    was_cached = COALESCE(excluded.was_cached, region_recent_session.was_cached),
    additional_outputs = COALESCE(excluded.additional_outputs, region_recent_session.additional_outputs);";
            cmd.Parameters.AddWithValue("$hash", regionHash);
            cmd.Parameters.AddWithValue("$completed", RegionDescriptorMapper.FormatDateTime(completedUtc));
            cmd.Parameters.AddWithValue("$duration", durationMs.HasValue ? durationMs.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$cached", wasCached.HasValue ? (wasCached.Value ? 1 : 0) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$outputs", (object?)outputsJson ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task TrimRecentSessionsAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
DELETE FROM region_recent_session
WHERE session_id NOT IN (
    SELECT session_id
    FROM region_recent_session
    ORDER BY completed_utc DESC, session_id DESC
    LIMIT $limit
);";
            command.Parameters.AddWithValue("$limit", SessionRetentionLimit);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task PersistDescriptorAsync(RegionDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = WorkspaceLayout.ExtractionDescriptorPath(_workspace, descriptor.RegionHash);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = RegionDescriptorMapper.SerializeDescriptor(descriptor);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }

        private void DeleteDescriptorArtifacts(RegionDescriptor descriptor)
        {
            var descriptorPath = WorkspaceLayout.ExtractionDescriptorPath(_workspace, descriptor.RegionHash);
            DeleteIfExists(descriptorPath);
            DeleteIfExists(descriptor.ImagePath);
            DeleteIfExists(descriptor.OcrTextPath);
            DeleteIfExists(descriptor.OfficePackagePath);
        }

        private void DeleteIfExists(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string absolute = Path.IsPathRooted(path)
                ? path
                : _workspace.GetAbsolutePath(path);

            if (File.Exists(absolute))
            {
                try { File.Delete(absolute); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string BuildMatchExpression(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;

            var tokens = query
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeToken)
                .Where(t => t.Length > 0)
                .Select(t => t + "*");

            return string.Join(" AND ", tokens);
        }

        private static string SanitizeToken(string token)
        {
            var builder = new StringBuilder(token.Length);
            foreach (var ch in token)
            {
                if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                    builder.Append(char.ToLowerInvariant(ch));
            }
            return builder.ToString();
        }
    }
}
