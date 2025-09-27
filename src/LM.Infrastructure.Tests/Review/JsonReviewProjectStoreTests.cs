using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Review;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Forms;
using Xunit;

namespace LM.Infrastructure.Tests.Review;

public sealed class JsonReviewProjectStoreTests
{
    [Fact]
    public async Task InitializeAsync_LoadsExistingProjectGraph()
    {
        using var workspace = new TempWorkspace();
        var store = await CreateStoreAsync(workspace.Path);

        var definition = CreateStageDefinition();
        var project = ReviewProject.Create(
            "proj-1",
            "Cardio Screening",
            DateTimeOffset.UtcNow,
            new[] { definition },
            ReviewAuditTrail.Create(new[]
            {
                ReviewAuditTrail.AuditEntry.Create("audit-1", "alice", "created", DateTimeOffset.UtcNow, "seed")
            }));

        await store.SaveProjectAsync(project);

        var assignment = ScreeningAssignment.Create(
            "assign-1",
            "stage-1",
            "reviewer-1",
            ReviewerRole.Primary,
            ScreeningStatus.Included,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow,
            ReviewerDecision.Create("assign-1", "reviewer-1", ScreeningStatus.Included, DateTimeOffset.UtcNow, "looks good"));

        var stage = ReviewStage.Create(
            "stage-1",
            project.Id,
            definition,
            new[] { assignment },
            ConflictState.Resolved,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            ConsensusOutcome.Create("stage-1", true, ConflictState.Resolved, DateTimeOffset.UtcNow, "approved", "alice"));

        await store.SaveStageAsync(stage);
        await store.SaveAssignmentAsync(project.Id, assignment);

        var snapshot = ExtractionFormSnapshot.Create(
            "extraction-form",
            "v1",
            new Dictionary<string, object?> { ["field1"] = "value" },
            "alice",
            DateTime.UtcNow);

        var response = new JsonReviewProjectStore.FormResponse("response-1", project.Id, stage.Id, assignment.Id, snapshot);
        await store.SaveFormResponseAsync(response);

        var hydrated = await CreateStoreAsync(workspace.Path);

        var loadedProject = await hydrated.GetProjectAsync(project.Id);
        Assert.NotNull(loadedProject);
        Assert.Equal(project.Name, loadedProject!.Name);

        var stages = await hydrated.GetStagesByProjectAsync(project.Id);
        var loadedStage = Assert.Single(stages);
        Assert.Equal(stage.Id, loadedStage.Id);
        Assert.Equal(stage.ConflictState, loadedStage.ConflictState);
        Assert.Single(loadedStage.Assignments);

        var assignments = await hydrated.GetAssignmentsByStageAsync(stage.Id);
        var loadedAssignment = Assert.Single(assignments);
        Assert.Equal(assignment.ReviewerId, loadedAssignment.ReviewerId);
        Assert.Equal(ScreeningStatus.Included, loadedAssignment.Status);

        var responses = await hydrated.GetFormResponsesAsync(project.Id);
        var loadedResponse = Assert.Single(responses);
        Assert.Equal(response.Id, loadedResponse.Id);
        Assert.Equal("value", loadedResponse.Snapshot.Values["field1"]);
    }

    [Fact]
    public async Task SaveStageAsync_AllowsConcurrentWritesForDifferentStages()
    {
        using var workspace = new TempWorkspace();
        var store = await CreateStoreAsync(workspace.Path);

        var definition = CreateStageDefinition();
        var project = ReviewProject.Create("proj-2", "Parallel", DateTimeOffset.UtcNow, new[] { definition }, ReviewAuditTrail.Create());
        await store.SaveProjectAsync(project);

        var stage1 = ReviewStage.Create(
            "stage-1",
            project.Id,
            definition,
            new[]
            {
                ScreeningAssignment.Create(
                    "assign-a",
                    "stage-1",
                    "reviewer-a",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    DateTimeOffset.UtcNow.AddMinutes(-15),
                    DateTimeOffset.UtcNow,
                    ReviewerDecision.Create("assign-a", "reviewer-a", ScreeningStatus.Included, DateTimeOffset.UtcNow))
            },
            ConflictState.None,
            DateTimeOffset.UtcNow,
            null,
            null);

        var stage2 = ReviewStage.Create(
            "stage-2",
            project.Id,
            definition,
            new[]
            {
                ScreeningAssignment.Create(
                    "assign-b",
                    "stage-2",
                    "reviewer-b",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    DateTimeOffset.UtcNow.AddMinutes(-10),
                    DateTimeOffset.UtcNow,
                    ReviewerDecision.Create("assign-b", "reviewer-b", ScreeningStatus.Included, DateTimeOffset.UtcNow))
            },
            ConflictState.None,
            DateTimeOffset.UtcNow,
            null,
            null);

        await Task.WhenAll(
            store.SaveStageAsync(stage1),
            store.SaveStageAsync(stage2));

        var stageIds = (await store.GetStagesByProjectAsync(project.Id)).Select(s => s.Id).ToArray();
        Assert.Contains("stage-1", stageIds);
        Assert.Contains("stage-2", stageIds);
    }

    [Fact]
    public async Task SaveProjectAsync_DoesNotCreateLockFile()
    {
        using var workspace = new TempWorkspace();
        var store = await CreateStoreAsync(workspace.Path);

        var definition = CreateStageDefinition();
        var project = ReviewProject.Create(
            "proj-3",
            "Lock Free",
            DateTimeOffset.UtcNow,
            new[] { definition },
            ReviewAuditTrail.Create());

        await store.SaveProjectAsync(project);

        var lockPath = Path.Combine(workspace.Path, "reviews", project.Id, "project.json.lock");
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public async Task SaveAssignmentAsync_RemovesLegacyLockFile()
    {
        using var workspace = new TempWorkspace();
        var store = await CreateStoreAsync(workspace.Path);

        var definition = CreateStageDefinition();
        var project = ReviewProject.Create("proj-4", "Legacy", DateTimeOffset.UtcNow, new[] { definition }, ReviewAuditTrail.Create());
        await store.SaveProjectAsync(project);

        var stage = ReviewStage.Create(
            "stage-1",
            project.Id,
            definition,
            new[]
            {
                ScreeningAssignment.Create(
                    "assign-1",
                    "stage-1",
                    "reviewer-1",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow,
                    ReviewerDecision.Create("assign-1", "reviewer-1", ScreeningStatus.Included, DateTimeOffset.UtcNow))
            },
            ConflictState.None,
            DateTimeOffset.UtcNow,
            null,
            null);

        await store.SaveStageAsync(stage);

        var assignment = ScreeningAssignment.Create(
            "assign-1",
            "stage-1",
            "reviewer-1",
            ReviewerRole.Primary,
            ScreeningStatus.Included,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow,
            ReviewerDecision.Create("assign-1", "reviewer-1", ScreeningStatus.Included, DateTimeOffset.UtcNow));

        var lockPath = Path.Combine(workspace.Path, "reviews", project.Id, "assignments", "assign-1.json.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        await File.WriteAllTextAsync(lockPath, string.Empty);

        await store.SaveAssignmentAsync(project.Id, assignment);

        Assert.False(File.Exists(lockPath));

        var stored = await store.GetAssignmentAsync("assign-1");
        Assert.NotNull(stored);
    }

    private static StageDefinition CreateStageDefinition()
    {
        var requirement = ReviewerRequirement.Create(new[]
        {
            new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1)
        });

        return StageDefinition.Create(
            "definition-1",
            "Title Screening",
            ReviewStageType.TitleScreening,
            requirement,
            StageConsensusPolicy.RequireAgreement(1, false, null));
    }

    private static async Task<JsonReviewProjectStore> CreateStoreAsync(string workspacePath)
    {
        var workspace = new WorkspaceService();
        await workspace.EnsureWorkspaceAsync(workspacePath);
        var store = new JsonReviewProjectStore(workspace);
        await store.InitializeAsync();
        return store;
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Path { get; }

        public TempWorkspace()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_review_store_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
