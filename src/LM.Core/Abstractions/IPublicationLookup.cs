// LM.Contracts (or LM.Infrastructure.Abstractions)
using LM.Core.Models;

public interface IPublicationLookup
{
    Task<PublicationRecord?> TryGetByDoiAsync(string doi, bool includeCitedBy, CancellationToken ct);
}
