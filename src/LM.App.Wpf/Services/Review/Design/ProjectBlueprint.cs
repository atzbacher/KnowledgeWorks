#nullable enable
using System;
using System.Collections.Generic;
using LM.Review.Core.Models;

namespace LM.App.Wpf.Services.Review.Design;

public sealed class ProjectBlueprint
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
        ReviewTemplateKind template,
        string metadataNotes,
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
        Template = template;
        MetadataNotes = string.IsNullOrWhiteSpace(metadataNotes)
            ? string.Empty
            : metadataNotes.Trim();
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

    public ReviewTemplateKind Template { get; }

    public string MetadataNotes { get; }

    public IReadOnlyList<StageBlueprint> Stages { get; }

    public ProjectBlueprint With(
        string? name = null,
        IReadOnlyList<StageBlueprint>? stages = null,
        ReviewTemplateKind? template = null,
        string? metadataNotes = null,
        string? litSearchEntryId = null,
        string? litSearchRunId = null,
        IReadOnlyList<string>? checkedEntryIds = null)
    {
        var resolvedName = string.IsNullOrWhiteSpace(name) ? Name : name.Trim();
        var resolvedStages = stages ?? Stages;
        var resolvedTemplate = template ?? Template;
        var resolvedNotes = string.IsNullOrWhiteSpace(metadataNotes)
            ? MetadataNotes
            : metadataNotes.Trim();
        var resolvedEntryId = string.IsNullOrWhiteSpace(litSearchEntryId) ? LitSearchEntryId : litSearchEntryId.Trim();
        var resolvedRunId = string.IsNullOrWhiteSpace(litSearchRunId) ? LitSearchRunId : litSearchRunId.Trim();
        var resolvedCheckedIds = checkedEntryIds ?? CheckedEntryIds;
        return new ProjectBlueprint(
            ProjectId,
            resolvedName,
            CreatedAtUtc,
            CreatedBy,
            resolvedEntryId,
            resolvedRunId,
            resolvedCheckedIds,
            HookRelativePath,
            resolvedTemplate,
            resolvedNotes,
            resolvedStages);
    }
}
