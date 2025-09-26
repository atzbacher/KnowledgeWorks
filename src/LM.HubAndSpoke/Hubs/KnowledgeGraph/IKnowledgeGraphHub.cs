#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LM.HubSpoke.Hubs.KnowledgeGraph
{
    public interface IKnowledgeGraphHub
    {
        Task InitializeAsync(CancellationToken ct = default);

        Task RefreshEntryAsync(string entryId, CancellationToken ct = default);

        Task<IReadOnlyList<MortalityComparison>> GetMortalityComparisonsAsync(string? entryId = null, CancellationToken ct = default);

        Task<IReadOnlyList<KaplanMeierOverlay>> GetKaplanMeierOverlaysAsync(string entryId, string? endpointId = null, CancellationToken ct = default);

        Task<IReadOnlyList<BaselineCharacteristicHit>> SearchBaselineCharacteristicsAsync(
            string characteristicSearchTerm,
            string? valueContains = null,
            CancellationToken ct = default);

        Task<GraphEntryOverview?> GetEntryOverviewAsync(string entryId, CancellationToken ct = default);
    }
}
