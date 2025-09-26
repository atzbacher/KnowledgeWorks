namespace LM.Review.Core.Models.Analytics;

public sealed record PrismaFlowSnapshot
{
    public PrismaFlowSnapshot(
        int recordsIdentified,
        int recordsScreened,
        int recordsIncluded,
        int recordsExcluded,
        int recordsEscalated,
        int pendingDecisions)
    {
        RecordsIdentified = recordsIdentified;
        RecordsScreened = recordsScreened;
        RecordsIncluded = recordsIncluded;
        RecordsExcluded = recordsExcluded;
        RecordsEscalated = recordsEscalated;
        PendingDecisions = pendingDecisions;
    }

    public int RecordsIdentified { get; }

    public int RecordsScreened { get; }

    public int RecordsIncluded { get; }

    public int RecordsExcluded { get; }

    public int RecordsEscalated { get; }

    public int PendingDecisions { get; }
}
