using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models;

public sealed class ReviewProject
{
    private ReviewProject(
        string id,
        string name,
        DateTimeOffset createdAt,
        IReadOnlyList<StageDefinition> stageDefinitions,
        ReviewAuditTrail auditTrail)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        StageDefinitions = stageDefinitions;
        AuditTrail = auditTrail;
    }

    public string Id { get; }

    public string Name { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<StageDefinition> StageDefinitions { get; }

    public ReviewAuditTrail AuditTrail { get; }

    public static ReviewProject Create(
        string id,
        string name,
        DateTimeOffset createdAtUtc,
        IEnumerable<StageDefinition> stageDefinitions,
        ReviewAuditTrail? auditTrail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(stageDefinitions);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        var trimmedId = id.Trim();
        var trimmedName = name.Trim();

        var definitionList = new List<StageDefinition>();
        foreach (var definition in stageDefinitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definitionList.Add(definition);
        }

        if (definitionList.Count == 0)
        {
            throw new InvalidOperationException("A review project must declare at least one stage definition.");
        }

        var duplicateStageIds = definitionList
            .GroupBy(definition => definition.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateStageIds.Count > 0)
        {
            throw new InvalidOperationException($"Stage definition identifiers must be unique. Duplicates: {string.Join(", ", duplicateStageIds)}");
        }

        var readOnlyDefinitions = new ReadOnlyCollection<StageDefinition>(definitionList);
        var resolvedAuditTrail = auditTrail ?? ReviewAuditTrail.Create();

        return new ReviewProject(trimmedId, trimmedName, createdAtUtc, readOnlyDefinitions, resolvedAuditTrail);
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be provided in UTC.", parameterName);
        }
    }
}
