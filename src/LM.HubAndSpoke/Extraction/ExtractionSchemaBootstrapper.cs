using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace LM.HubSpoke.Extraction
{
    public static class ExtractionSchemaBootstrapper
    {
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
            await ExecuteAsync(connection, DescriptorIndexesSql, cancellationToken);
            await ExecuteAsync(connection, RecentSessionTableSql, cancellationToken);
            await ExecuteAsync(connection, RecentSessionIndexesSql, cancellationToken);
            await EnsureFtsTableAsync(connection, cancellationToken);
        }

        private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task EnsureFtsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE VIRTUAL TABLE region_descriptor_fts USING fts5(
    region_hash UNINDEXED,
    entry_hub_id UNINDEXED,
    source_rel_path UNINDEXED,
    ocr_text,
    notes,
    annotation,
    tags
);";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException ex) when (
                ex.SqliteErrorCode == 1 &&
                ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Table already exists; nothing to do.
            }
        }
    }
}
