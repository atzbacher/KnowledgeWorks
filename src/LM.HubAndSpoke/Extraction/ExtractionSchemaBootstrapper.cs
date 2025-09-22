using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace LM.HubSpoke.Extraction
{
    public static class ExtractionSchemaBootstrapper
    {
        private const string DefaultTimestampSql = "'1970-01-01T00:00:00Z'";
        private const string DefaultBoundsSql = "'[0,0,0,0]'";


        private const string DescriptorTableSql = @"
CREATE TABLE IF NOT EXISTS region_descriptor (
    region_hash TEXT PRIMARY KEY,
    entry_hub_id TEXT NOT NULL,
    source_rel_path TEXT NOT NULL,
    source_sha256 TEXT,
    page_number INTEGER,
    bounds TEXT NOT NULL,
    ocr_text TEXT,
    tags TEXT,
    notes TEXT,
    annotation TEXT,
    created_utc TEXT NOT NULL,
    updated_utc TEXT,
    last_export_status TEXT NOT NULL,
    last_error_message TEXT,
    office_package_path TEXT,
    image_path TEXT,
    ocr_text_path TEXT,
    exporter_id TEXT,
    extra_metadata TEXT
);";

        private const string DescriptorIndexesSql = @"
CREATE INDEX IF NOT EXISTS idx_region_descriptor_entry ON region_descriptor(entry_hub_id);
CREATE INDEX IF NOT EXISTS idx_region_descriptor_created ON region_descriptor(created_utc);
CREATE INDEX IF NOT EXISTS idx_region_descriptor_updated ON region_descriptor(updated_utc);
";

        private const string RecentSessionTableSql = @"
CREATE TABLE IF NOT EXISTS region_recent_session (
    session_id INTEGER PRIMARY KEY AUTOINCREMENT,
    region_hash TEXT NOT NULL,
    completed_utc TEXT NOT NULL,
    duration_ms INTEGER,
    was_cached INTEGER,
    additional_outputs TEXT,
    UNIQUE(region_hash, completed_utc)
);";

        private const string RecentSessionIndexesSql = @"
CREATE INDEX IF NOT EXISTS idx_region_recent_session_hash ON region_recent_session(region_hash);
CREATE INDEX IF NOT EXISTS idx_region_recent_session_completed ON region_recent_session(completed_utc);
";

        private static readonly ColumnDefinition[] DescriptorColumns =
        {
            new("entry_hub_id", "TEXT"),
            new("source_rel_path", "TEXT"),
            new("source_sha256", "TEXT"),
            new("page_number", "INTEGER"),
            new("bounds", "TEXT", DefaultBoundsSql),
            new("ocr_text", "TEXT"),
            new("tags", "TEXT"),
            new("notes", "TEXT"),
            new("annotation", "TEXT"),
            new("created_utc", "TEXT", DefaultTimestampSql),
            new("updated_utc", "TEXT"),
            new("last_export_status", "TEXT", "'Pending'"),
            new("last_error_message", "TEXT"),
            new("office_package_path", "TEXT"),
            new("image_path", "TEXT"),
            new("ocr_text_path", "TEXT"),
            new("exporter_id", "TEXT"),
            new("extra_metadata", "TEXT")
        };

        private static readonly ColumnDefinition[] RecentSessionColumns =
        {
            new("duration_ms", "INTEGER"),
            new("was_cached", "INTEGER"),
            new("additional_outputs", "TEXT")
        };

        private static readonly string[] FtsColumns =
        {
            "region_hash",
            "entry_hub_id",
            "source_rel_path",
            "ocr_text",
            "notes",
            "annotation",
            "tags"
        };

        private static readonly JsonSerializerOptions TagJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };


        public static async Task EnsureAsync(IWorkSpaceService workspace, CancellationToken cancellationToken = default)
        {
            if (workspace is null) throw new ArgumentNullException(nameof(workspace));

            var dbPath = workspace.GetLocalDbPath();
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;");
            await connection.OpenAsync(cancellationToken);

            await using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                await pragma.ExecuteNonQueryAsync(cancellationToken);
            }

            await ExecuteAsync(connection, DescriptorTableSql, cancellationToken);

            await EnsureColumnsAsync(connection, "region_descriptor", DescriptorColumns, cancellationToken);
            await ExecuteAsync(connection, DescriptorIndexesSql, cancellationToken);

            await ExecuteAsync(connection, RecentSessionTableSql, cancellationToken);
            await EnsureColumnsAsync(connection, "region_recent_session", RecentSessionColumns, cancellationToken);
            await ExecuteAsync(connection, RecentSessionIndexesSql, cancellationToken);

            await EnsureFtsTableAsync(connection, cancellationToken);
        }

        private static async Task ExecuteAsync(
            SqliteConnection connection,
            string sql,
            CancellationToken cancellationToken,
            SqliteTransaction? transaction = null)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }


        private static async Task EnsureColumnsAsync(
            SqliteConnection connection,
            string table,
            IEnumerable<ColumnDefinition> columns,
            CancellationToken cancellationToken)
        {
            var existing = await GetColumnNamesAsync(connection, table, cancellationToken);

            foreach (var column in columns)
            {
                if (existing.Contains(column.Name))
                    continue;

                var sql = $"ALTER TABLE {table} ADD COLUMN {column.Name} {column.Type}";
                if (!string.IsNullOrWhiteSpace(column.DefaultSql))
                    sql += $" DEFAULT {column.DefaultSql}";
                sql += ";";

                await ExecuteAsync(connection, sql, cancellationToken);
            }
        }

        private static async Task EnsureFtsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            var exists = await TableExistsAsync(connection, "region_descriptor_fts", cancellationToken);
            var existingColumns = exists
                ? await GetColumnNamesAsync(connection, "region_descriptor_fts", cancellationToken)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (exists && FtsColumns.All(existingColumns.Contains))
                return;

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                if (exists)
                    await ExecuteAsync(connection, "DROP TABLE IF EXISTS region_descriptor_fts;", cancellationToken, transaction);

                await CreateFtsTableAsync(connection, cancellationToken, transaction);
                await PopulateFtsAsync(connection, cancellationToken, transaction);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static async Task CreateFtsTableAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken,
            SqliteTransaction? transaction)
        {
            await ExecuteAsync(connection, @"

CREATE VIRTUAL TABLE region_descriptor_fts USING fts5(
    region_hash UNINDEXED,
    entry_hub_id UNINDEXED,
    source_rel_path UNINDEXED,
    ocr_text,
    notes,
    annotation,
    tags

);", cancellationToken, transaction);
        }

        private static async Task PopulateFtsAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken,
            SqliteTransaction? transaction)
        {
            await using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = @"
SELECT region_hash, entry_hub_id, source_rel_path, ocr_text, notes, annotation, tags
FROM region_descriptor;";

            await using var reader = await select.ExecuteReaderAsync(cancellationToken);

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO region_descriptor_fts(
    region_hash, entry_hub_id, source_rel_path, ocr_text, notes, annotation, tags)
VALUES($hash, $entry, $source, $ocr, $notes, $annotation, $tags);";

            var hashParam = insert.CreateParameter();
            hashParam.ParameterName = "$hash";
            insert.Parameters.Add(hashParam);

            var entryParam = insert.CreateParameter();
            entryParam.ParameterName = "$entry";
            insert.Parameters.Add(entryParam);

            var sourceParam = insert.CreateParameter();
            sourceParam.ParameterName = "$source";
            insert.Parameters.Add(sourceParam);

            var ocrParam = insert.CreateParameter();
            ocrParam.ParameterName = "$ocr";
            insert.Parameters.Add(ocrParam);

            var notesParam = insert.CreateParameter();
            notesParam.ParameterName = "$notes";
            insert.Parameters.Add(notesParam);

            var annotationParam = insert.CreateParameter();
            annotationParam.ParameterName = "$annotation";
            insert.Parameters.Add(annotationParam);

            var tagsParam = insert.CreateParameter();
            tagsParam.ParameterName = "$tags";
            insert.Parameters.Add(tagsParam);

            while (await reader.ReadAsync(cancellationToken))
            {
                hashParam.Value = reader.GetString(0);
                entryParam.Value = reader.GetString(1);
                sourceParam.Value = reader.GetString(2);
                ocrParam.Value = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                notesParam.Value = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                annotationParam.Value = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                tagsParam.Value = BuildFtsTagString(reader.IsDBNull(6) ? null : reader.GetString(6));

                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private static async Task<HashSet<string>> GetColumnNamesAsync(
            SqliteConnection connection,
            string table,
            CancellationToken cancellationToken)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({table});";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            return names;
        }

        private static async Task<bool> TableExistsAsync(
            SqliteConnection connection,
            string table,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$table LIMIT 1;";
            command.Parameters.AddWithValue("$table", table);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private static string BuildFtsTagString(string? tagsJson)
        {
            if (string.IsNullOrWhiteSpace(tagsJson))
                return string.Empty;

            try
            {
                var tags = JsonSerializer.Deserialize<string[]>(tagsJson, TagJsonOptions) ?? Array.Empty<string>();
                return string.Join(' ', tags
                    .Select(t => t?.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Select(t => t!.ToLowerInvariant()));
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private readonly record struct ColumnDefinition(string Name, string Type, string? DefaultSql = null);

    }
}
