#nullable enable
using System;
using System.Collections.Generic;

namespace LM.App.Wpf.Services.Review.Design;

internal sealed class ProjectBlueprint
{
    public ProjectBlueprint(
        string projectId,
        string name,
        DateTimeOffset createdAtUtc,
        string createdBy,
        string litSearchEntryId,
        string litSearchRunId,
        IReadOnlyList<string> checkedEntryIds,
        string? hookRelativePath,
        IReadOnlyList<StageBlueprint> stages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(litSearchEntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(litSearchRunId);
        ArgumentNullException.ThrowIfNull(checkedEntryIds);
        ArgumentNullException.ThrowIfNull(stages);

        ProjectId = projectId.Trim();
        Name = name.Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy.Trim();
        LitSearchEntryId = litSearchEntryId.Trim();
        LitSearchRunId = litSearchRunId.Trim();
        CheckedEntryIds = checkedEntryIds;
        HookRelativePath = hookRelativePath;
        Stages = stages;
    }

    public string ProjectId { get; }

    public string Name { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string CreatedBy { get; }

    public string LitSearchEntryId { get; }

    public string LitSearchRunId { get; }

    public IReadOnlyList<string> CheckedEntryIds { get; }

    public string? HookRelativePath { get; }

    public IReadOnlyList<StageBlueprint> Stages { get; }

    public ProjectBlueprint With(string? name = null, IReadOnlyList<StageBlueprint>? stages = null)
    {
        var resolvedName = string.IsNullOrWhiteSpace(name) ? Name : name.Trim();
        var resolvedStages = stages ?? Stages;
        return new ProjectBlueprint(
            ProjectId,
            resolvedName,
            CreatedAtUtc,
            CreatedBy,
            LitSearchEntryId,
            LitSearchRunId,
            CheckedEntryIds,
            HookRelativePath,
            resolvedStages);
    }
}
