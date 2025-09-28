#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models;

public sealed class StageDisplayProfile
{
    private StageDisplayProfile(ReadOnlyCollection<StageContentArea> contentAreas)
    {
        ContentAreas = contentAreas;
    }

    public IReadOnlyList<StageContentArea> ContentAreas { get; }

    public static StageDisplayProfile Create(IEnumerable<StageContentArea> contentAreas)
    {
        ArgumentNullException.ThrowIfNull(contentAreas);

        var normalized = new List<StageContentArea>();
        foreach (var area in contentAreas)
        {
            if (!normalized.Contains(area))
            {
                normalized.Add(area);
            }
        }

        if (normalized.Count == 0)
        {
            throw new InvalidOperationException("A stage display profile must include at least one content area.");
        }

        return new StageDisplayProfile(new ReadOnlyCollection<StageContentArea>(normalized));
    }
}
