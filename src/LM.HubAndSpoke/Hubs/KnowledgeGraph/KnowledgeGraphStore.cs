#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.Models;
using Microsoft.Data.Sqlite;

namespace LM.HubSpoke.Hubs.KnowledgeGraph
{
    internal sealed class KnowledgeGraphStore
    {
        private const string PaperNodeType = "Paper";
        private const string PopulationNodeType = "Population";
        private const string InterventionNodeType = "Intervention";
        private const string EndpointNodeType = "Endpoint";

        private const string PaperHasPopulation = "HAS_POPULATION";
        private const string PopulationHasIntervention = "HAS_INTERVENTION";
        private const string InterventionReportsEndpoint = "REPORTS_ENDPOINT";
        private const string InterventionComparedWith = "COMPARED_WITH";

        private readonly IWorkSpaceService _workspace;
        private readonly string _databasePath;

        public KnowledgeGraphStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            var root = WorkspaceRoot();
            Directory.CreateDirectory(root);
            _databasePath = Path.Combine(root, "knowledge_graph.db");
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            var statements = new[]
            {
                "CREATE TABLE IF NOT EXISTS Papers (EntryId TEXT PRIMARY KEY, Title TEXT NOT NULL, Year INTEGER NULL, Source TEXT NULL)",
                "CREATE TABLE IF NOT EXISTS Populations (EntryId TEXT NOT NULL, PopulationId TEXT NOT NULL, Name TEXT NOT NULL, Description TEXT NULL, PRIMARY KEY (EntryId, PopulationId))",
                "CREATE TABLE IF NOT EXISTS BaselineCharacteristics (EntryId TEXT NOT NULL, PopulationId TEXT NOT NULL, Characteristic TEXT NOT NULL, Value TEXT NULL, PRIMARY KEY (EntryId, PopulationId, Characteristic))",
                "CREATE TABLE IF NOT EXISTS Interventions (EntryId TEXT NOT NULL, InterventionId TEXT NOT NULL, Name TEXT NOT NULL, Type TEXT NULL, Description TEXT NULL, PRIMARY KEY (EntryId, InterventionId))",
                "CREATE TABLE IF NOT EXISTS Endpoints (EntryId TEXT NOT NULL, EndpointId TEXT NOT NULL, Name TEXT NOT NULL, Category TEXT NOT NULL, Description TEXT NULL, PRIMARY KEY (EntryId, EndpointId))",
                "CREATE TABLE IF NOT EXISTS EndpointReadouts (EntryId TEXT NOT NULL, EndpointId TEXT NOT NULL, InterventionId TEXT NULL, ComparatorInterventionId TEXT NULL, PopulationId TEXT NULL, Metric TEXT NULL, Value REAL NULL, Unit TEXT NULL, Timepoint TEXT NULL, CurveJson TEXT NULL, PRIMARY KEY (EntryId, EndpointId, InterventionId, ComparatorInterventionId, PopulationId, Metric, Timepoint))",
                "CREATE TABLE IF NOT EXISTS GraphEdges (EntryId TEXT NOT NULL, SourceType TEXT NOT NULL, SourceId TEXT NOT NULL, TargetType TEXT NOT NULL, TargetId TEXT NOT NULL, Relationship TEXT NOT NULL, PayloadJson TEXT NULL, PRIMARY KEY (EntryId, SourceType, SourceId, TargetType, TargetId, Relationship))",
                "CREATE INDEX IF NOT EXISTS IX_Baseline_Search ON BaselineCharacteristics (Characteristic, Value)",
                "CREATE INDEX IF NOT EXISTS IX_EndPoint_Category ON Endpoints (Category)",
                "CREATE INDEX IF NOT EXISTS IX_Readouts_Category ON EndpointReadouts (EntryId, EndpointId)"
            };

            foreach (var sql in statements)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task ReplaceEntryAsync(EntryHub hub, DataExtractionHook hook, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(hub);
            ArgumentNullException.ThrowIfNull(hook);

            var entryId = hub.EntryId;
            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            await DeleteEntryInternalAsync(connection, transaction, entryId, ct).ConfigureAwait(false);
            await InsertPaperAsync(connection, transaction, hub, hook, ct).ConfigureAwait(false);
            await InsertPopulationsAsync(connection, transaction, entryId, hook.Populations, ct).ConfigureAwait(false);
            await InsertInterventionsAsync(connection, transaction, entryId, hook.Interventions, hook.Assignments, ct).ConfigureAwait(false);
            await InsertEndpointsAsync(connection, transaction, entryId, hook.Endpoints, ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        public async Task DeleteEntryAsync(string entryId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return;

            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await DeleteEntryInternalAsync(connection, transaction, entryId, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<MortalityComparison>> QueryMortalityComparisonsAsync(string? entryId, CancellationToken ct)
        {
            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            const string sql = "SELECT r.EntryId, p.Title, r.EndpointId, e.Name, r.PopulationId, r.InterventionId, i.Name, r.ComparatorInterventionId, ci.Name, r.Value, r.Unit, r.Metric, r.Timepoint FROM EndpointReadouts r JOIN Endpoints e ON e.EntryId = r.EntryId AND e.EndpointId = r.EndpointId JOIN Papers p ON p.EntryId = r.EntryId LEFT JOIN Interventions i ON i.EntryId = r.EntryId AND i.InterventionId = r.InterventionId LEFT JOIN Interventions ci ON ci.EntryId = r.EntryId AND ci.InterventionId = r.ComparatorInterventionId WHERE lower(e.Category) = 'mortality' AND ($entryId IS NULL OR r.EntryId = $entryId)";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var param = command.CreateParameter();
            param.ParameterName = "$entryId";
            param.Value = entryId ?? (object)DBNull.Value;
            command.Parameters.Add(param);

            var results = new List<MortalityComparison>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(new MortalityComparison(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetDouble(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12)));
            }

            return results;
        }

        public async Task<IReadOnlyList<KaplanMeierOverlay>> QueryKaplanMeierAsync(string entryId, string? endpointId, CancellationToken ct)
        {
            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            const string sql = "SELECT r.EntryId, p.Title, r.EndpointId, e.Name, r.PopulationId, r.InterventionId, i.Name, r.CurveJson FROM EndpointReadouts r JOIN Endpoints e ON e.EntryId = r.EntryId AND e.EndpointId = r.EndpointId JOIN Papers p ON p.EntryId = r.EntryId LEFT JOIN Interventions i ON i.EntryId = r.EntryId AND i.InterventionId = r.InterventionId WHERE r.CurveJson IS NOT NULL AND r.EntryId = $entryId AND ($endpointId IS NULL OR r.EndpointId = $endpointId)";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var entryParam = command.CreateParameter();
            entryParam.ParameterName = "$entryId";
            entryParam.Value = entryId;
            command.Parameters.Add(entryParam);
            var endpointParam = command.CreateParameter();
            endpointParam.ParameterName = "$endpointId";
            endpointParam.Value = endpointId ?? (object)DBNull.Value;
            command.Parameters.Add(endpointParam);

            var overlays = new List<KaplanMeierOverlay>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var curveJson = reader.GetString(7);
                var curve = JsonSerializer.Deserialize<List<KaplanMeierPointDto>>(curveJson, JsonStd.Options)
                            ?? new List<KaplanMeierPointDto>();
                overlays.Add(new KaplanMeierOverlay(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    curve));
            }

            return overlays;
        }

        public async Task<IReadOnlyList<BaselineCharacteristicHit>> QueryBaselineAsync(string characteristicTerm, string? valueTerm, CancellationToken ct)
        {
            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            const string sql = "SELECT bc.EntryId, p.Title, bc.PopulationId, pop.Name, bc.Characteristic, bc.Value FROM BaselineCharacteristics bc JOIN Papers p ON p.EntryId = bc.EntryId LEFT JOIN Populations pop ON pop.EntryId = bc.EntryId AND pop.PopulationId = bc.PopulationId WHERE lower(bc.Characteristic) LIKE $char AND ($val IS NULL OR lower(IFNULL(bc.Value, '')) LIKE $val)";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var charParam = command.CreateParameter();
            charParam.ParameterName = "$char";
            charParam.Value = $"%{characteristicTerm.ToLowerInvariant()}%";
            command.Parameters.Add(charParam);
            var valParam = command.CreateParameter();
            valParam.ParameterName = "$val";
            if (string.IsNullOrWhiteSpace(valueTerm))
            {
                valParam.Value = DBNull.Value;
            }
            else
            {
                valParam.Value = $"%{valueTerm.ToLowerInvariant()}%";
            }
            command.Parameters.Add(valParam);

            var hits = new List<BaselineCharacteristicHit>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                hits.Add(new BaselineCharacteristicHit(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? string.Empty : reader.GetString(5)));
            }

            return hits;
        }

        public async Task<GraphEntryOverview?> LoadEntryOverviewAsync(string entryId, CancellationToken ct)
        {
            await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
            const string paperSql = "SELECT Title FROM Papers WHERE EntryId = $entryId";
            await using var paperCommand = connection.CreateCommand();
            paperCommand.CommandText = paperSql;
            var idParam = paperCommand.CreateParameter();
            idParam.ParameterName = "$entryId";
            idParam.Value = entryId;
            paperCommand.Parameters.Add(idParam);
            var title = (string?)await paperCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (title is null)
                return null;

            var populations = await LoadPopulationsAsync(connection, entryId, ct).ConfigureAwait(false);
            var interventions = await LoadInterventionsAsync(connection, entryId, ct).ConfigureAwait(false);
            var endpoints = await LoadEndpointsAsync(connection, entryId, ct).ConfigureAwait(false);
            var edges = await LoadEdgesAsync(connection, entryId, ct).ConfigureAwait(false);

            return new GraphEntryOverview(entryId, title, populations, interventions, endpoints, edges);
        }

        private string WorkspaceRoot()
        {
            var extractionRoot = LM.HubSpoke.FileSystem.WorkspaceLayout.ExtractionRoot(_workspace);
            Directory.CreateDirectory(extractionRoot);
            return extractionRoot;
        }

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private
            }.ToString();

            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return connection;
        }

        private static async Task DeleteEntryInternalAsync(SqliteConnection connection, SqliteTransaction transaction, string entryId, CancellationToken ct)
        {
            var tables = new[]
            {
                "GraphEdges",
                "EndpointReadouts",
                "Endpoints",
                "BaselineCharacteristics",
                "Populations",
                "Interventions",
                "Papers"
            };

            foreach (var table in tables)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"DELETE FROM {table} WHERE EntryId = $entryId";
                var param = command.CreateParameter();
                param.ParameterName = "$entryId";
                param.Value = entryId;
                command.Parameters.Add(param);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        private static async Task InsertPaperAsync(SqliteConnection connection, SqliteTransaction transaction, EntryHub hub, DataExtractionHook hook, CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Papers (EntryId, Title, Year, Source) VALUES ($entryId, $title, $year, $source)";
            AddParam(command, "$entryId", hub.EntryId);
            AddParam(command, "$title", hook.Title ?? hub.DisplayTitle);
            AddParam(command, "$year", hook.Year);
            AddParam(command, "$source", hook.Source);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        private async Task InsertPopulationsAsync(SqliteConnection connection, SqliteTransaction transaction, string entryId, IReadOnlyList<ExtractedPopulation> populations, CancellationToken ct)
        {
            foreach (var population in populations ?? Array.Empty<ExtractedPopulation>())
            {
                ct.ThrowIfCancellationRequested();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO Populations (EntryId, PopulationId, Name, Description) VALUES ($entryId, $id, $name, $desc)";
                AddParam(command, "$entryId", entryId);
                AddParam(command, "$id", population.Id);
                AddParam(command, "$name", population.Name);
                AddParam(command, "$desc", population.Description);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                await InsertEdgeAsync(connection, transaction, entryId, PaperNodeType, entryId, PopulationNodeType, population.Id, PaperHasPopulation, payload: null, ct).ConfigureAwait(false);

                if (population.BaselineCharacteristics is not null)
                {
                    foreach (var kvp in population.BaselineCharacteristics)
                    {
                        ct.ThrowIfCancellationRequested();
                        await using var baselineCommand = connection.CreateCommand();
                        baselineCommand.Transaction = transaction;
                        baselineCommand.CommandText = "INSERT INTO BaselineCharacteristics (EntryId, PopulationId, Characteristic, Value) VALUES ($entryId, $popId, $char, $val)";
                        AddParam(baselineCommand, "$entryId", entryId);
                        AddParam(baselineCommand, "$popId", population.Id);
                        AddParam(baselineCommand, "$char", kvp.Key);
                        AddParam(baselineCommand, "$val", kvp.Value);
                        await baselineCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task InsertInterventionsAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            IReadOnlyList<ExtractedIntervention> interventions,
            IReadOnlyList<PopulationInterventionAssignment> assignments,
            CancellationToken ct)
        {
            var interventionSet = interventions ?? Array.Empty<ExtractedIntervention>();
            foreach (var intervention in interventionSet)
            {
                ct.ThrowIfCancellationRequested();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO Interventions (EntryId, InterventionId, Name, Type, Description) VALUES ($entryId, $id, $name, $type, $desc)";
                AddParam(command, "$entryId", entryId);
                AddParam(command, "$id", intervention.Id);
                AddParam(command, "$name", intervention.Name);
                AddParam(command, "$type", intervention.Type);
                AddParam(command, "$desc", intervention.Description);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var assignment in assignments ?? Array.Empty<PopulationInterventionAssignment>())
            {
                ct.ThrowIfCancellationRequested();
                await InsertEdgeAsync(
                    connection,
                    transaction,
                    entryId,
                    PopulationNodeType,
                    assignment.PopulationId,
                    InterventionNodeType,
                    assignment.InterventionId,
                    PopulationHasIntervention,
                    payload: assignment.ArmLabel is null ? null : JsonSerializer.Serialize(new { label = assignment.ArmLabel }, JsonStd.Options),
                    ct).ConfigureAwait(false);
            }
        }

        private async Task InsertEndpointsAsync(SqliteConnection connection, SqliteTransaction transaction, string entryId, IReadOnlyList<ExtractedEndpoint> endpoints, CancellationToken ct)
        {
            foreach (var endpoint in endpoints ?? Array.Empty<ExtractedEndpoint>())
            {
                ct.ThrowIfCancellationRequested();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO Endpoints (EntryId, EndpointId, Name, Category, Description) VALUES ($entryId, $id, $name, $cat, $desc)";
                AddParam(command, "$entryId", entryId);
                AddParam(command, "$id", endpoint.Id);
                AddParam(command, "$name", endpoint.Name);
                AddParam(command, "$cat", endpoint.Category);
                AddParam(command, "$desc", endpoint.Description);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                foreach (var readout in endpoint.Readouts ?? Array.Empty<EndpointReadout>())
                {
                    ct.ThrowIfCancellationRequested();
                    var curveJson = readout.KaplanMeierCurve is null
                        ? null
                        : JsonSerializer.Serialize(readout.KaplanMeierCurve.Select(p => new KaplanMeierPointDto(p.Time, p.SurvivalProbability)).ToList(), JsonStd.Options);

                    await using var readoutCommand = connection.CreateCommand();
                    readoutCommand.Transaction = transaction;
                    readoutCommand.CommandText = "INSERT INTO EndpointReadouts (EntryId, EndpointId, InterventionId, ComparatorInterventionId, PopulationId, Metric, Value, Unit, Timepoint, CurveJson) VALUES ($entryId, $endpointId, $interventionId, $compId, $populationId, $metric, $value, $unit, $timepoint, $curve)";
                    AddParam(readoutCommand, "$entryId", entryId);
                    AddParam(readoutCommand, "$endpointId", endpoint.Id);
                    AddParam(readoutCommand, "$interventionId", readout.InterventionId);
                    AddParam(readoutCommand, "$compId", readout.ComparatorInterventionId);
                    AddParam(readoutCommand, "$populationId", readout.PopulationId);
                    AddParam(readoutCommand, "$metric", readout.Metric);
                    AddParam(readoutCommand, "$value", readout.Value);
                    AddParam(readoutCommand, "$unit", readout.Unit);
                    AddParam(readoutCommand, "$timepoint", readout.Timepoint);
                    AddParam(readoutCommand, "$curve", curveJson);
                    await readoutCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(readout.InterventionId))
                    {
                        await InsertEdgeAsync(
                            connection,
                            transaction,
                            entryId,
                            InterventionNodeType,
                            readout.InterventionId!,
                            EndpointNodeType,
                            endpoint.Id,
                            InterventionReportsEndpoint,
                            payload: JsonSerializer.Serialize(new
                            {
                                readout.PopulationId,
                                readout.Metric,
                                readout.Timepoint,
                                readout.Value,
                                readout.Unit
                            }, JsonStd.Options),
                            ct).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(readout.ComparatorInterventionId) && !string.Equals(readout.ComparatorInterventionId, readout.InterventionId, StringComparison.Ordinal))
                        {
                            await InsertEdgeAsync(
                                connection,
                                transaction,
                                entryId,
                                InterventionNodeType,
                                readout.InterventionId!,
                                InterventionNodeType,
                                readout.ComparatorInterventionId!,
                                InterventionComparedWith,
                                payload: JsonSerializer.Serialize(new
                                {
                                    endpointId = endpoint.Id,
                                    readout.Timepoint,
                                    readout.Metric
                                }, JsonStd.Options),
                                ct).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private static async Task InsertEdgeAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            string sourceType,
            string sourceId,
            string targetType,
            string targetId,
            string relationship,
            string? payload,
            CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT OR REPLACE INTO GraphEdges (EntryId, SourceType, SourceId, TargetType, TargetId, Relationship, PayloadJson) VALUES ($entryId, $sourceType, $sourceId, $targetType, $targetId, $rel, $payload)";
            AddParam(command, "$entryId", entryId);
            AddParam(command, "$sourceType", sourceType);
            AddParam(command, "$sourceId", sourceId);
            AddParam(command, "$targetType", targetType);
            AddParam(command, "$targetId", targetId);
            AddParam(command, "$rel", relationship);
            AddParam(command, "$payload", payload);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<GraphPopulationNode>> LoadPopulationsAsync(SqliteConnection connection, string entryId, CancellationToken ct)
        {
            const string sql = "SELECT PopulationId, Name, Description FROM Populations WHERE EntryId = $entryId ORDER BY PopulationId";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParam(command, "$entryId", entryId);
            var list = new List<GraphPopulationNode>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new GraphPopulationNode(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
            return list;
        }

        private static async Task<IReadOnlyList<GraphInterventionNode>> LoadInterventionsAsync(SqliteConnection connection, string entryId, CancellationToken ct)
        {
            const string sql = "SELECT InterventionId, Name, Type, Description FROM Interventions WHERE EntryId = $entryId ORDER BY Name COLLATE NOCASE, InterventionId";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParam(command, "$entryId", entryId);
            var list = new List<GraphInterventionNode>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new GraphInterventionNode(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
            return list;
        }

        private static async Task<IReadOnlyList<GraphEndpointNode>> LoadEndpointsAsync(SqliteConnection connection, string entryId, CancellationToken ct)
        {
            const string sql = "SELECT EndpointId, Name, Category, Description FROM Endpoints WHERE EntryId = $entryId ORDER BY EndpointId";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParam(command, "$entryId", entryId);
            var list = new List<GraphEndpointNode>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new GraphEndpointNode(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
            return list;
        }

        private static async Task<IReadOnlyList<GraphEdge>> LoadEdgesAsync(SqliteConnection connection, string entryId, CancellationToken ct)
        {
            const string sql = "SELECT SourceType, SourceId, TargetType, TargetId, Relationship, PayloadJson FROM GraphEdges WHERE EntryId = $entryId ORDER BY SourceType, SourceId";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParam(command, "$entryId", entryId);
            var list = new List<GraphEdge>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new GraphEdge(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }
            return list;
        }

        private static void AddParam(SqliteCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
