using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;
using Microsoft.Data.Sqlite;

namespace LM.Infrastructure.Extraction
{
    public sealed partial class SqliteExtractionRepository
    {
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
    }
}
