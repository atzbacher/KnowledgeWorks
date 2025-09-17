#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.Abstractions;
using Microsoft.Data.Sqlite;

namespace LM.HubSpoke.Indexing
{
    public sealed class SimilarityLog : ISimilarityLog
    {
        private readonly IWorkSpaceService _ws;
        private readonly string _dbPath;

        public SimilarityLog(IWorkSpaceService ws)
        {
            _ws = ws ?? throw new ArgumentNullException(nameof(ws));
            _dbPath = ws.GetLocalDbPath();
        }

        public string NewSessionId() => Guid.NewGuid().ToString("N");

        public async Task LogAsync(string sessionId,
                                   string stagedPath,
                                   string candidateEntryId,
                                   double score,
                                   string method,
                                   CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

            await using var c = new SqliteConnection($"Data Source={_dbPath};Cache=Shared;");
            await c.OpenAsync(ct);

            await using (var ddl = c.CreateCommand())
            {
                ddl.CommandText = @"
CREATE TABLE IF NOT EXISTS similarity_log(
  session_id TEXT NOT NULL,
  staged     TEXT NOT NULL,
  candidate_entry_id TEXT NOT NULL,
  score      REAL NOT NULL,
  method     TEXT NOT NULL,
  created_utc TEXT NOT NULL,
  PRIMARY KEY(session_id, candidate_entry_id)
);";
                await ddl.ExecuteNonQueryAsync(ct);
            }

            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO similarity_log(session_id, staged, candidate_entry_id, score, method, created_utc)
VALUES($sid,$staged,$cid,$score,$method,$utc)
ON CONFLICT(session_id, candidate_entry_id) DO UPDATE SET
  score=excluded.score, method=excluded.method, created_utc=excluded.created_utc;";
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.Parameters.AddWithValue("$staged", stagedPath);
            cmd.Parameters.AddWithValue("$cid", candidateEntryId);
            cmd.Parameters.AddWithValue("$score", score);
            cmd.Parameters.AddWithValue("$method", method);
            cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
