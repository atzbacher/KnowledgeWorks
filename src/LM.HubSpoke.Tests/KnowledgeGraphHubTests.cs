#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Hubs.KnowledgeGraph;
using LM.HubSpoke.Models;
using LM.HubSpoke.Spokes;
using Xunit;

namespace LM.HubSpoke.Tests
{
    public sealed class KnowledgeGraphHubTests
    {
        [Fact]
        public async Task RefreshEntryAsync_BuildsGraphAndSupportsQueries()
        {
            using var temp = new TempWorkspace();
            var workspace = new TestWorkspaceService(temp.RootPath);
            var entryId = "01HXGRAPH0001";
            var extractionRelPath = Path.Combine("extraction", "00", "11", "sample.json");

            await WriteHubAsync(temp.RootPath, entryId, extractionRelPath);
            await WriteExtractionAsync(temp.RootPath, extractionRelPath, entryId);

            var graph = new KnowledgeGraphHub(workspace);
            await graph.InitializeAsync();
            await graph.RefreshEntryAsync(entryId);

            var overview = await graph.GetEntryOverviewAsync(entryId);
            Assert.NotNull(overview);
            Assert.Equal(entryId, overview!.EntryId);
            Assert.Single(overview.Populations);
            Assert.Equal("pop-itt", overview.Populations[0].PopulationId);
            Assert.Equal("Drug A", overview.Interventions[0].Name);
            Assert.Equal(2, overview.Interventions.Count);
            Assert.Equal(2, overview.Endpoints.Count);
            Assert.Contains(overview.Edges, e => e.Relationship == "HAS_POPULATION");

            var mortality = await graph.GetMortalityComparisonsAsync(entryId);
            Assert.Single(mortality);
            var mortalityResult = mortality[0];
            Assert.Equal(-0.05, mortalityResult.Value);
            Assert.Equal("arm-drug", mortalityResult.InterventionId);
            Assert.Equal("arm-control", mortalityResult.ComparatorInterventionId);

            var km = await graph.GetKaplanMeierOverlaysAsync(entryId, "km-overall");
            Assert.Equal(2, km.Count);
            Assert.All(km, overlay => Assert.NotEmpty(overlay.Curve));

            var baseline = await graph.SearchBaselineCharacteristicsAsync("age", valueContains: "62");
            Assert.Single(baseline);
            Assert.Equal("Age, mean", baseline[0].Characteristic);
        }

        [Fact]
        public async Task RefreshEntryAsync_PrunesMissingExtraction()
        {
            using var temp = new TempWorkspace();
            var workspace = new TestWorkspaceService(temp.RootPath);
            var entryId = "01HXGRAPH0002";
            var extractionRelPath = Path.Combine("extraction", "ab", "cd", "missing.json");

            await WriteHubAsync(temp.RootPath, entryId, extractionRelPath);
            await WriteExtractionAsync(temp.RootPath, extractionRelPath, entryId);

            var graph = new KnowledgeGraphHub(workspace);
            await graph.RefreshEntryAsync(entryId);
            Assert.NotNull(await graph.GetEntryOverviewAsync(entryId));

            var extractionPath = Path.Combine(temp.RootPath, extractionRelPath);
            File.Delete(extractionPath);

            await graph.RefreshEntryAsync(entryId);
            Assert.Null(await graph.GetEntryOverviewAsync(entryId));
            Assert.Empty(await graph.GetMortalityComparisonsAsync(entryId));
        }

        [Fact]
        public async Task HubSpokeStore_NotifiesGraphOnSave()
        {
            using var temp = new TempWorkspace();
            var workspace = new TestWorkspaceService(temp.RootPath);
            var graph = new RecordingGraphHub();
            var store = new HubSpokeStore(
                workspace,
                new NoopHasher(),
                new ISpokeHandler[] { new LitSearchSpokeHandler(workspace) },
                contentExtractor: null,
                graphHub: graph);

            await store.InitializeAsync();

            var entry = new Entry
            {
                Title = "Lit search",
                Type = EntryType.LitSearch,
                AddedBy = "CONTOSO\\UserA"
            };

            await store.SaveAsync(entry);

            Assert.Single(graph.RefreshedIds);
            Assert.Equal(entry.Id, graph.RefreshedIds[0]);
        }

        private static async Task WriteHubAsync(string root, string entryId, string extractionRelPath)
        {
            var hub = new EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Graph Trial",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                CreatedBy = new PersonRef("user", "user"),
                UpdatedBy = new PersonRef("user", "user"),
                Hooks = new EntryHooks
                {
                    DataExtraction = extractionRelPath
                }
            };

            var entryDir = Path.Combine(root, "entries", entryId);
            Directory.CreateDirectory(entryDir);
            var json = JsonSerializer.Serialize(hub, JsonStd.Options);
            await File.WriteAllTextAsync(Path.Combine(entryDir, "hub.json"), json);
        }

        private static async Task WriteExtractionAsync(string root, string relativePath, string entryId)
        {
            var hook = new DataExtractionHook
            {
                EntryId = entryId,
                Title = "Graph Trial",
                Year = 2024,
                Source = "JAMA",
                Populations = new List<ExtractedPopulation>
                {
                    new()
                    {
                        Id = "pop-itt",
                        Name = "Intent-to-treat",
                        BaselineCharacteristics = new Dictionary<string, string>
                        {
                            ["Age, mean"] = "62.1 years",
                            ["Male (%)"] = "58"
                        }
                    }
                },
                Interventions = new List<ExtractedIntervention>
                {
                    new()
                    {
                        Id = "arm-drug",
                        Name = "Drug A",
                        Type = "Active"
                    },
                    new()
                    {
                        Id = "arm-control",
                        Name = "Standard of care",
                        Type = "Control"
                    }
                },
                Assignments = new List<PopulationInterventionAssignment>
                {
                    new()
                    {
                        PopulationId = "pop-itt",
                        InterventionId = "arm-drug",
                        ArmLabel = "Drug A"
                    },
                    new()
                    {
                        PopulationId = "pop-itt",
                        InterventionId = "arm-control",
                        ArmLabel = "Control"
                    }
                },
                Endpoints = new List<ExtractedEndpoint>
                {
                    new()
                    {
                        Id = "mortality-30d",
                        Name = "30-day mortality",
                        Category = "mortality",
                        Readouts = new List<EndpointReadout>
                        {
                            new()
                            {
                                PopulationId = "pop-itt",
                                InterventionId = "arm-drug",
                                ComparatorInterventionId = "arm-control",
                                Metric = "RiskDifference",
                                Value = -0.05,
                                Unit = "absolute",
                                Timepoint = "30 days"
                            }
                        }
                    },
                    new()
                    {
                        Id = "km-overall",
                        Name = "Overall survival",
                        Category = "kaplan_meier",
                        Readouts = new List<EndpointReadout>
                        {
                            new()
                            {
                                PopulationId = "pop-itt",
                                InterventionId = "arm-drug",
                                Metric = "KM",
                                KaplanMeierCurve = new List<KaplanMeierPoint>
                                {
                                    new() { Time = 0, SurvivalProbability = 1.0 },
                                    new() { Time = 30, SurvivalProbability = 0.9 }
                                }
                            },
                            new()
                            {
                                PopulationId = "pop-itt",
                                InterventionId = "arm-control",
                                Metric = "KM",
                                KaplanMeierCurve = new List<KaplanMeierPoint>
                                {
                                    new() { Time = 0, SurvivalProbability = 1.0 },
                                    new() { Time = 30, SurvivalProbability = 0.85 }
                                }
                            }
                        }
                    }
                }
            };

            var absPath = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
            var json = JsonSerializer.Serialize(hook, JsonStd.Options);
            await File.WriteAllTextAsync(absPath, json);
        }

        private sealed class RecordingGraphHub : IKnowledgeGraphHub
        {
            public List<string> RefreshedIds { get; } = new();

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task RefreshEntryAsync(string entryId, CancellationToken ct = default)
            {
                RefreshedIds.Add(entryId);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<MortalityComparison>> GetMortalityComparisonsAsync(string? entryId = null, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<MortalityComparison>>(Array.Empty<MortalityComparison>());

            public Task<IReadOnlyList<KaplanMeierOverlay>> GetKaplanMeierOverlaysAsync(string entryId, string? endpointId = null, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<KaplanMeierOverlay>>(Array.Empty<KaplanMeierOverlay>());

            public Task<IReadOnlyList<BaselineCharacteristicHit>> SearchBaselineCharacteristicsAsync(string characteristicSearchTerm, string? valueContains = null, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<BaselineCharacteristicHit>>(Array.Empty<BaselineCharacteristicHit>());

            public Task<GraphEntryOverview?> GetEntryOverviewAsync(string entryId, CancellationToken ct = default)
                => Task.FromResult<GraphEntryOverview?>(null);
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
                RootPath = Path.Combine(Path.GetTempPath(), "kw-graph-tests", Guid.NewGuid().ToString("N"));
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
                }
            }
        }
    }
}
