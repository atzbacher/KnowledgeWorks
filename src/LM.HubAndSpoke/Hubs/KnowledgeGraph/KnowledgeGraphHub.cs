#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.Models;

namespace LM.HubSpoke.Hubs.KnowledgeGraph
{
    public sealed class KnowledgeGraphHub : IKnowledgeGraphHub
    {
        private readonly IWorkSpaceService _workspace;
        private readonly KnowledgeGraphStore _store;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private volatile bool _initialized;

        public KnowledgeGraphHub(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _store = new KnowledgeGraphStore(workspace);
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
                return;

            await _initLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_initialized)
                    return;

                await _store.InitializeAsync(ct).ConfigureAwait(false);
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task RefreshEntryAsync(string entryId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be provided.", nameof(entryId));

            await EnsureInitializedAsync(ct).ConfigureAwait(false);

            var hub = await HubJsonStore.LoadAsync(_workspace, entryId, ct).ConfigureAwait(false);
            if (hub is null)
            {
                await _store.DeleteEntryAsync(entryId, ct).ConfigureAwait(false);
                return;
            }

            var relPath = hub.Hooks?.DataExtraction;
            if (string.IsNullOrWhiteSpace(relPath))
            {
                await _store.DeleteEntryAsync(entryId, ct).ConfigureAwait(false);
                return;
            }

            var absPath = Path.IsPathRooted(relPath)
                ? relPath
                : _workspace.GetAbsolutePath(relPath);
            if (!File.Exists(absPath))
            {
                await _store.DeleteEntryAsync(entryId, ct).ConfigureAwait(false);
                return;
            }

            DataExtractionHook? hook;
            try
            {
                var json = await File.ReadAllTextAsync(absPath, ct).ConfigureAwait(false);
                hook = JsonSerializer.Deserialize<DataExtractionHook>(json, JsonStd.Options);
            }
            catch
            {
                hook = null;
            }

            if (hook is null)
            {
                await _store.DeleteEntryAsync(entryId, ct).ConfigureAwait(false);
                return;
            }

            await _store.ReplaceEntryAsync(hub, hook, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<MortalityComparison>> GetMortalityComparisonsAsync(string? entryId = null, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            return await _store.QueryMortalityComparisonsAsync(entryId, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<KaplanMeierOverlay>> GetKaplanMeierOverlaysAsync(string entryId, string? endpointId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be provided.", nameof(entryId));

            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            return await _store.QueryKaplanMeierAsync(entryId, endpointId, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<BaselineCharacteristicHit>> SearchBaselineCharacteristicsAsync(string characteristicSearchTerm, string? valueContains = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(characteristicSearchTerm))
                throw new ArgumentException("Characteristic search term must be provided.", nameof(characteristicSearchTerm));

            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            return await _store.QueryBaselineAsync(characteristicSearchTerm, valueContains, ct).ConfigureAwait(false);
        }

        public async Task<GraphEntryOverview?> GetEntryOverviewAsync(string entryId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must be provided.", nameof(entryId));

            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            return await _store.LoadEntryOverviewAsync(entryId, ct).ConfigureAwait(false);
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized)
                return;

            await InitializeAsync(ct).ConfigureAwait(false);
        }
    }
}
