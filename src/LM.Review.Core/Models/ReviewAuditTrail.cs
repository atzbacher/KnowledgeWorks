using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models;

public sealed class ReviewAuditTrail
{
    private ReviewAuditTrail(IReadOnlyList<AuditEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<AuditEntry> Entries { get; }

    public static ReviewAuditTrail Create(IEnumerable<AuditEntry>? entries = null)
    {
        var entryList = new List<AuditEntry>();
        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                ArgumentNullException.ThrowIfNull(entry);
                entryList.Add(entry);
            }
        }

        var ordered = entryList
            .OrderBy(entry => entry.OccurredAt)
            .ToList();

        return new ReviewAuditTrail(new ReadOnlyCollection<AuditEntry>(ordered));
    }

    public ReviewAuditTrail Append(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var combined = Entries
            .Concat(new[] { entry })
            .OrderBy(e => e.OccurredAt)
            .ToList();

        return new ReviewAuditTrail(new ReadOnlyCollection<AuditEntry>(combined));
    }

    public sealed record AuditEntry
    {
        private AuditEntry(string id, string actor, string action, DateTimeOffset occurredAt, string? details)
        {
            Id = id;
            Actor = actor;
            Action = action;
            OccurredAt = occurredAt;
            Details = details;
        }

        public string Id { get; }

        public string Actor { get; }

        public string Action { get; }

        public DateTimeOffset OccurredAt { get; }

        public string? Details { get; }

        public static AuditEntry Create(string id, string actor, string action, DateTimeOffset occurredAtUtc, string? details = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);
            ArgumentException.ThrowIfNullOrWhiteSpace(action);
            EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

            return new AuditEntry(id.Trim(), actor.Trim(), action.Trim(), occurredAtUtc, details?.Trim());
        }
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be provided in UTC.", parameterName);
        }
    }
}
