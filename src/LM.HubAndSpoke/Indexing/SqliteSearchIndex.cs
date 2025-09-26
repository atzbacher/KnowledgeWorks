#nullable enable
using LM.Core.Abstractions;            // IWorkSpaceService
using LM.Core.Models;
using LM.Core.Models.Filters;         // EntryFilter
using LM.Core.Models.Search;
using Microsoft.Data.Sqlite;          // SqliteConnection, SqliteTransaction
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace LM.HubSpoke.Indexing
{
    internal sealed class SqliteSearchIndex : IFullTextSearchService
    {
        private readonly IWorkSpaceService _ws;
        private readonly string _dbPath;

        private static readonly (FullTextSearchField Flag, string Column)[] FieldColumnMap = new[]
        {
            (FullTextSearchField.Title, "title"),
            (FullTextSearchField.Abstract, "abstract"),
            (FullTextSearchField.Content, "content")
        };

        private static readonly string[] DefaultMatchColumns = FieldColumnMap.Select(f => f.Column).ToArray();

        internal readonly record struct IndexRecord(
            string EntryId,
            string DisplayTitle,
            int? Year,
            bool IsInternal,
            string? Type,
            string? Doi,
            string? Pmid,
            string? Journal,
            string? Title,
            string? Abstract,
            IReadOnlyList<string> Authors,
            IReadOnlyList<string> Keywords,
            IReadOnlyList<string> Tags,
            IReadOnlyList<string> AssetHashes,
            string? Content
        );

        public readonly record struct SearchHit(string EntryId, double Score);

        public SqliteSearchIndex(IWorkSpaceService ws)
        {
            _ws = ws;
            _dbPath = ws.GetLocalDbPath();
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

            await using var c = new SqliteConnection($"Data Source={_dbPath};Cache=Shared;");
            await c.OpenAsync(ct);

            // 1) WAL mode (safe to run every time)
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 2) Core tables (create one-by-one to avoid batch failures)
            await using (var t1 = c.CreateCommand())
            {
                t1.CommandText = @"
CREATE TABLE IF NOT EXISTS entries(
  entry_id TEXT PRIMARY KEY,
  display_title TEXT NOT NULL,
  year INTEGER,
  is_internal INTEGER NOT NULL,
  type TEXT,
  doi TEXT,
  pmid TEXT,
  journal TEXT
);";
                await t1.ExecuteNonQueryAsync(ct);
            }

            await using (var t2 = c.CreateCommand())
            {
                t2.CommandText = @"
CREATE TABLE IF NOT EXISTS entry_tags(
  entry_id TEXT NOT NULL,
  tag TEXT NOT NULL,
  PRIMARY KEY(entry_id, tag)
);";
                await t2.ExecuteNonQueryAsync(ct);
            }

            await using (var t3 = c.CreateCommand())
            {
                t3.CommandText = @"
CREATE TABLE IF NOT EXISTS entry_authors(
  entry_id TEXT NOT NULL,
  author TEXT NOT NULL,
  PRIMARY KEY(entry_id, author)
);";
                await t3.ExecuteNonQueryAsync(ct);
            }

            await using (var t4 = c.CreateCommand())
            {
                t4.CommandText = @"
CREATE TABLE IF NOT EXISTS asset_hash(
  sha256 TEXT NOT NULL,
  entry_id TEXT NOT NULL,
  PRIMARY KEY(sha256, entry_id)
);";
                await t4.ExecuteNonQueryAsync(ct);
            }

            // 3) FTS virtual table — some SQLite builds reject "IF NOT EXISTS" on virtual tables.
            // Create once and ignore "already exists".
            try
            {
                await using var v = c.CreateCommand();
                v.CommandText = @"
CREATE VIRTUAL TABLE entry_text USING fts5(
  entry_id UNINDEXED,
  title,
  abstract,
  authors,
  keywords,
  journal,
  content
);";
                await v.ExecuteNonQueryAsync(ct);
            }
            catch (SqliteException ex) when (
                ex.SqliteErrorCode == 1 &&
                ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // entry_text already created – ignore
            }

            // 4) Ensure new columns (safe on fresh DB)
            await EnsureTypeColumnAsync(c, ct);

            // 5) Helpful indexes (no-op if already there)
            await using (var idx = c.CreateCommand())
            {
                idx.CommandText = @"
CREATE INDEX IF NOT EXISTS idx_entries_type     ON entries(type);
CREATE INDEX IF NOT EXISTS idx_entries_year     ON entries(year);
CREATE INDEX IF NOT EXISTS idx_entries_internal ON entries(is_internal);";
                await idx.ExecuteNonQueryAsync(ct);
            }
        }


        public async Task UpsertAsync(IndexRecord r, CancellationToken ct = default)
        {

            try
            {
                await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;");
            await c.OpenAsync(ct);

            // NOTE: Use synchronous begin/commit to get a SqliteTransaction (not DbTransaction)
            using var tx = c.BeginTransaction();

                // entries
                await ExecAsync(c, tx, @"
INSERT INTO entries(entry_id, display_title, year, is_internal, type, doi, pmid, journal)
VALUES($id,$t,$y,$i,$ty,$doi,$pmid,$jr)
ON CONFLICT(entry_id) DO UPDATE SET
 display_title=$t, year=$y, is_internal=$i, type=$ty, doi=$doi, pmid=$pmid, journal=$jr;",
                    new Dictionary<string, object?>
                    {
                        ["$id"] = r.EntryId,
                        ["$t"] = r.DisplayTitle,
                        ["$y"] = r.Year,
                        ["$i"] = r.IsInternal ? 1 : 0,
                        ["$ty"] = (object?)r.Type ?? DBNull.Value,
                        ["$doi"] = r.Doi,
                        ["$pmid"] = r.Pmid,
                        ["$jr"] = r.Journal
                    }, ct);

                // tags
                await ExecAsync(
                c, tx,
                "DELETE FROM entry_tags WHERE entry_id=$id;",
                new Dictionary<string, object?> { ["$id"] = r.EntryId },
                ct);

            foreach (var tag in r.Tags)
            {
                await ExecAsync(
                    c, tx,
                    "INSERT OR IGNORE INTO entry_tags(entry_id, tag) VALUES($id,$t);",
                    new Dictionary<string, object?> { ["$id"] = r.EntryId, ["$t"] = tag },
                    ct);
            }

            // authors
            await ExecAsync(
                c, tx,
                "DELETE FROM entry_authors WHERE entry_id=$id;",
                new Dictionary<string, object?> { ["$id"] = r.EntryId },
                ct);

            foreach (var a in r.Authors)
            {
                await ExecAsync(
                    c, tx,
                    "INSERT OR IGNORE INTO entry_authors(entry_id, author) VALUES($id,$a);",
                    new Dictionary<string, object?> { ["$id"] = r.EntryId, ["$a"] = a },
                    ct);
            }

            // text (FTS)
            await ExecAsync(
                c, tx,
                "DELETE FROM entry_text WHERE entry_id=$id;",
                new Dictionary<string, object?> { ["$id"] = r.EntryId },
                ct);

            await ExecAsync(
                c, tx,
                @"
INSERT INTO entry_text(entry_id, title, abstract, authors, keywords, journal, content)
VALUES($id,$t,$ab,$au,$kw,$jr,$ct);",
                new Dictionary<string, object?>
                {
                    ["$id"] = r.EntryId,
                    ["$t"] = r.Title ?? string.Empty,
                    ["$ab"] = r.Abstract ?? string.Empty,
                    ["$au"] = string.Join(", ", r.Authors),
                    ["$kw"] = string.Join(", ", r.Keywords),
                    ["$jr"] = r.Journal ?? string.Empty,
                    ["$ct"] = r.Content ?? string.Empty
                },
                ct);

            // asset -> entry mapping
            foreach (var h in r.AssetHashes)
            {
                await ExecAsync(
                    c, tx,
                    "INSERT OR IGNORE INTO asset_hash(sha256, entry_id) VALUES($sha,$id);",
                    new Dictionary<string, object?> { ["$sha"] = h, ["$id"] = r.EntryId },
                    ct);
            }

            // Commit (sync to match the sync transaction)
            tx.Commit();
                await TraceAsync($"UPSERT OK id={r.EntryId}, title={r.Title ?? r.DisplayTitle}", null, ct);

            }


            catch (Exception ex)
       
            {
            await TraceAsync($"UPSERT FAIL id={r.EntryId}", ex, ct);
            throw; // surface it so you notice during dev
            }

        }
        public async Task<IReadOnlyList<SearchHit>> SearchAsync(EntryFilter f, CancellationToken ct = default)
        {
            await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;");
            await c.OpenAsync(ct);

            // 1) Build a safe FTS expression from sanitized tokens
            var matchExpr = BuildMatchExpression(f);

            // 2) Build SQL (JOIN when matching; plain table scan when not)
            string sql;
            if (!string.IsNullOrEmpty(matchExpr))
            {
                var escaped = matchExpr.Replace("'", "''"); // inline only after escaping
                sql = $@"
SELECT e.entry_id, bm25(entry_text) AS score
FROM entry_text
JOIN entries e ON e.entry_id = entry_text.entry_id
WHERE entry_text MATCH '{escaped}'
  AND ( $yf IS NULL OR e.year >= $yf )
  AND ( $yt IS NULL OR e.year <= $yt )
  AND ( $int IS NULL OR e.is_internal = $int )
ORDER BY score ASC
LIMIT 500;";
            }
            else
            {
                // No tokens → skip FTS; return rows filtered by year/internal only.
                sql = @"
SELECT e.entry_id, 1000.0 AS score
FROM entries e
WHERE ( $yf IS NULL OR e.year >= $yf )
  AND ( $yt IS NULL OR e.year <= $yt )
  AND ( $int IS NULL OR e.is_internal = $int )
ORDER BY e.display_title ASC
LIMIT 500;";
            }

            var types = (f.TypesAny as EntryType[])
                        ?? f.TypesAny?.ToArray()
                        ?? Array.Empty<EntryType>();

            string whereTypes = "";
            if (types.Length > 0)
            {
                var placeholders = string.Join(",", Enumerable.Range(0, types.Length).Select(i => $"$tp{i}"));
                whereTypes = $" AND e.type IN ({placeholders})";
            }


            await using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$yf", (object?)f.YearFrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$yt", (object?)f.YearTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$int", f.IsInternal.HasValue ? (f.IsInternal.Value ? 1 : 0) : (object)DBNull.Value);

            if (types.Length > 0)
            {
                for (int i = 0; i < types.Length; i++)
                    cmd.Parameters.AddWithValue($"$tp{i}", types[i].ToString());
            }

            var hits = new List<SearchHit>();

            try
            {
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    hits.Add(new SearchHit(r.GetString(0), r.GetDouble(1)));
            }
            catch (SqliteException ex)
            {
                // Defensive: never crash the Add flow because of a bad FTS string
                System.Diagnostics.Debug.WriteLine($"[SqliteSearchIndex] MATCH failed: {ex.Message}; expr='{matchExpr}'");
                return Array.Empty<SearchHit>();
            }

            return hits;
        }

        public async Task<IReadOnlyList<FullTextSearchHit>> SearchAsync(FullTextSearchQuery query, CancellationToken ct = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var tokens = TokenizeForFts(query.Text ?? string.Empty);
            if (tokens.Count == 0)
                return Array.Empty<FullTextSearchHit>();

            var columns = ResolveColumns(query.Fields);
            var matchExpr = BuildMatchExpression(tokens, columns);

            await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;");
            await c.OpenAsync(ct);

            var types = (query.TypesAny as EntryType[])
                        ?? query.TypesAny?.ToArray()
                        ?? Array.Empty<EntryType>();

            var whereTypes = BuildTypesWhereClause(types);

            var sql = @"
SELECT e.entry_id,
       bm25(entry_text) AS raw_score,
       snippet(entry_text, 'title', '[', ']', '…', 12) AS snip_title,
       snippet(entry_text, 'abstract', '[', ']', '…', 12) AS snip_abstract,
       snippet(entry_text, 'content', '[', ']', '…', 20) AS snip_content,
       entry_text.title   AS plain_title,
       entry_text.abstract AS plain_abstract,
       entry_text.content  AS plain_content
FROM entry_text
JOIN entries e ON e.entry_id = entry_text.entry_id
WHERE entry_text MATCH $match
  AND ( $yf IS NULL OR e.year >= $yf )
  AND ( $yt IS NULL OR e.year <= $yt )
  AND ( $int IS NULL OR e.is_internal = $int )" + whereTypes + @"
ORDER BY raw_score ASC
LIMIT $limit;";

            await using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$match", matchExpr);
            cmd.Parameters.AddWithValue("$yf", (object?)query.YearFrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$yt", (object?)query.YearTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$int", query.IsInternal.HasValue ? (query.IsInternal.Value ? 1 : 0) : (object)DBNull.Value);

            var limit = query.Limit <= 0 ? 100 : Math.Clamp(query.Limit, 1, 500);
            cmd.Parameters.AddWithValue("$limit", limit);

            if (types.Length > 0)
            {
                for (int i = 0; i < types.Length; i++)
                    cmd.Parameters.AddWithValue($"$tp{i}", types[i].ToString());
            }

            var hits = new List<FullTextSearchHit>();

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var entryId = reader.GetString(0);
                    var rawScore = reader.IsDBNull(1) ? 0d : reader.GetDouble(1);
                    var highlight = PickHighlight(reader, columns, tokens);
                    hits.Add(new FullTextSearchHit(entryId, NormalizeScore(rawScore), highlight));
                }
            }
            catch (SqliteException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqliteSearchIndex] Full-text search failed: {ex.Message}; expr='{matchExpr}'");
                return Array.Empty<FullTextSearchHit>();
            }

            return hits;
        }


        public async Task<IReadOnlyList<string>> FindByHashAsync(string sha256, CancellationToken ct = default)
        {
            await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;");
            await c.OpenAsync(ct);

            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT entry_id FROM asset_hash WHERE sha256=$h LIMIT 100;";
            cmd.Parameters.AddWithValue("$h", sha256);

            var ids = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                ids.Add(r.GetString(0));
            }
            return ids;
        }

        private static string FTS(string s)
        {
            // case-insensitive prefix search
            var t = s.ToLowerInvariant().Replace("\"", "\"\"");
            return $"{t}*";
        }

        private static async Task ExecAsync(
            SqliteConnection c,
            SqliteTransaction tx,
            string sql,
            IReadOnlyDictionary<string, object?> args,
            CancellationToken ct)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach (var kv in args)
                cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task ClearAsync(CancellationToken ct = default)
        {
            await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;");
            await c.OpenAsync(ct);
            await using var tx = c.BeginTransaction();
            foreach (var tbl in new[] { "entry_text", "entry_tags", "entry_authors", "asset_hash", "entries" })
            {
                using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"DELETE FROM {tbl};";
                await cmd.ExecuteNonQueryAsync(ct);
            }
            tx.Commit();
        }

        private async Task TraceAsync(string msg, Exception? ex = null, CancellationToken ct = default)
        {
            try
            {
                var dir = Path.GetDirectoryName(_dbPath);
                var logPath = Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, "index.log");
                var line = $"{DateTime.UtcNow:O} [{Environment.ProcessId}] {msg}" +
                           (ex != null ? Environment.NewLine + ex + Environment.NewLine : Environment.NewLine);
                await File.AppendAllTextAsync(logPath, line, ct);
            }
            catch { /* ignore logging errors */ }
        }

        public async Task<(string DisplayTitle, int? Year, bool IsInternal, string? Doi, string? Pmid, string? Journal)?>
    TryReadEntryRowAsync(string entryId, CancellationToken ct = default)
        {
            await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;");
            await c.OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT display_title, year, is_internal, doi, pmid, journal
                        FROM entries WHERE entry_id=$id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", entryId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                var title = r.IsDBNull(0) ? "(untitled)" : r.GetString(0);
                var year = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
                var intl = !r.IsDBNull(2) && r.GetInt32(2) == 1;
                var doi = r.IsDBNull(3) ? null : r.GetString(3);
                var pmid = r.IsDBNull(4) ? null : r.GetString(4);
                var jr = r.IsDBNull(5) ? null : r.GetString(5);
                return (title, year, intl, doi, pmid, jr);
            }
            return null;
        }

        public async Task<IReadOnlyList<string>> GetAssetHashesAsync(string entryId, CancellationToken ct = default)
        {
            await using var c = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;");
            await c.OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT sha256 FROM asset_hash WHERE entry_id=$id;";
            cmd.Parameters.AddWithValue("$id", entryId);
            var list = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(r.GetString(0));
            return list;
        }

        private static IReadOnlyList<string> ResolveColumns(FullTextSearchField fields)
        {
            if (fields == FullTextSearchField.None)
                return DefaultMatchColumns;

            var columns = new List<string>(FieldColumnMap.Length);
            foreach (var (flag, column) in FieldColumnMap)
            {
                if (fields.HasFlag(flag))
                    columns.Add(column);
            }

            return columns.Count == 0 ? DefaultMatchColumns : columns;
        }

        private static string BuildMatchExpression(IReadOnlyList<string> tokens, IReadOnlyList<string> columns)
        {
            if (tokens.Count == 0 || columns.Count == 0)
                return string.Empty;

            var clauses = new List<string>(tokens.Count);
            foreach (var token in tokens)
            {
                var perColumn = columns.Select(column => $"{column}:{token}*");
                clauses.Add($"({string.Join(" OR ", perColumn)})");
            }

            return string.Join(" AND ", clauses);
        }

        private static string BuildTypesWhereClause(IReadOnlyList<EntryType> types)
        {
            if (types.Count == 0)
                return string.Empty;

            var placeholders = string.Join(",", Enumerable.Range(0, types.Count).Select(i => $"$tp{i}"));
            return $"\n  AND e.type IN ({placeholders})";
        }

        private static string? PickHighlight(SqliteDataReader reader, IReadOnlyList<string> preferredColumns, IReadOnlyList<string> tokens)
        {
            static string? Value(SqliteDataReader r, int index)
                => r.IsDBNull(index) ? null : r.GetString(index);

            var map = new Dictionary<string, (string? Snippet, string? Plain)>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = (Value(reader, 2), Value(reader, 5)),
                ["abstract"] = (Value(reader, 3), Value(reader, 6)),
                ["content"] = (Value(reader, 4), Value(reader, 7))
            };

            foreach (var column in preferredColumns)
            {
                if (!map.TryGetValue(column, out var pair))
                    continue;

                var highlight = EnsureHighlight(pair.Snippet, pair.Plain, tokens);
                if (!string.IsNullOrWhiteSpace(highlight))
                    return highlight;
            }

            foreach (var pair in map.Values)
            {
                var highlight = EnsureHighlight(pair.Snippet, pair.Plain, tokens);
                if (!string.IsNullOrWhiteSpace(highlight))
                    return highlight;
            }

            return null;
        }

        private static string? EnsureHighlight(string? snippet, string? plain, IReadOnlyList<string> tokens)
        {
            var candidate = EnsureHighlightContainsMarker(snippet, tokens);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;

            return EnsureHighlightContainsMarker(plain, tokens);
        }

        private static string? EnsureHighlightContainsMarker(string? snippet, IReadOnlyList<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(snippet))
                return snippet;

            var hasMarker = snippet.IndexOf('[', StringComparison.Ordinal) >= 0
                && snippet.IndexOf(']', StringComparison.Ordinal) > snippet.IndexOf('[', StringComparison.Ordinal);
            if (hasMarker)
                return snippet;

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                var index = snippet.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    continue;

                var before = snippet[..index];
                var match = snippet.Substring(index, token.Length);
                var after = snippet[(index + token.Length)..];
                return string.Concat(before, "[", match, "]", after);
            }

            return null;
        }

        private static double NormalizeScore(double rawScore)
        {
            if (double.IsNaN(rawScore) || double.IsInfinity(rawScore))
                return 0d;

            var clamped = Math.Max(0d, rawScore);
            return 1d / (1d + clamped);
        }

        private static string? BuildMatchExpression(EntryFilter f)
        {
            var clauses = new List<string>();

            if (!string.IsNullOrWhiteSpace(f.TitleContains))
            {
                var words = TokenizeForFts(f.TitleContains!);
                if (words.Count > 0)
                {
                    // (title:w* OR abstract:w* OR content:w*) AND ...
                    var perToken = words.Select(w => $"(title:{w}* OR abstract:{w}* OR content:{w}*)");
                    clauses.Add(string.Join(" AND ", perToken));
                }
            }

            if (!string.IsNullOrWhiteSpace(f.AuthorContains))
            {
                var words = TokenizeForFts(f.AuthorContains!);
                if (words.Count > 0)
                {
                    // authors:w* AND ...
                    var perToken = words.Select(w => $"authors:{w}*");
                    clauses.Add(string.Join(" AND ", perToken));
                }
            }

            if (clauses.Count == 0) return null;
            return string.Join(" AND ", clauses);
        }

        private static async Task<bool> ColumnExistsAsync(SqliteConnection c, string table, string column, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static async Task EnsureTypeColumnAsync(SqliteConnection c, CancellationToken ct)
        {
            // Check if 'type' column already exists on 'entries'
            await using var check = c.CreateCommand();
            check.CommandText = "PRAGMA table_info(entries);";
            await using var r = await check.ExecuteReaderAsync(ct);

            var hasType = false;
            while (await r.ReadAsync(ct))
            {
                // PRAGMA table_info returns: cid,name,type,notnull,dflt_value,pk
                var colName = r.GetString(1);
                if (string.Equals(colName, "type", StringComparison.OrdinalIgnoreCase))
                {
                    hasType = true;
                    break;
                }
            }

            if (!hasType)
            {
                // Add the column
                await using (var alter = c.CreateCommand())
                {
                    alter.CommandText = "ALTER TABLE entries ADD COLUMN type TEXT;";
                    await alter.ExecuteNonQueryAsync(ct);
                }
            }

            // Make sure we have an index on 'type' (no-op if it exists)
            await using (var idx = c.CreateCommand())
            {
                idx.CommandText = "CREATE INDEX IF NOT EXISTS idx_entries_type ON entries(type);";
                await idx.ExecuteNonQueryAsync(ct);
            }
        }


        private static List<string> TokenizeForFts(string s)
        {
            // Keep only letters/digits; everything else becomes space; lowercase; distinct; take a few tokens
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');

            return sb.ToString()
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Where(w => w.Length >= 2)
                     .Distinct()
                     .Take(6)
                     .ToList();
        }


    }
}
