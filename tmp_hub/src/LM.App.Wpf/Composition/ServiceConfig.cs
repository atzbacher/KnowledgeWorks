#nullable enable
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.Entries;
using LM.HubSpoke.Spokes;
using LM.Infrastructure.Content;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Metadata;
using LM.Infrastructure.PubMed;
using LM.Infrastructure.Storage;
using LM.Infrastructure.Utils;
using System;
using System.Linq;

namespace LM.App.Wpf.Composition
{
    /// <summary>Self-contained, tiny application wiring helper.</summary>
    internal static class ServiceConfig
    {
        internal sealed class AppServices
        {
            public required IWorkSpaceService Workspaces { get; init; }
            public required IFileStorageRepository Storage { get; init; }
            public required IHasher Hasher { get; init; }
            public required IContentExtractor ContentExtractor { get; init; }
            public required ISimilarityService Similarity { get; init; }
            public required IMetadataExtractor Metadata { get; init; }
            public required IDoiNormalizer Doi { get; init; }
            public required IPmidNormalizer Pmid { get; init; }
            public required IPublicationLookup PubMed { get; init; }
            public required IEntryStore Store { get; init; }
            public required HookOrchestrator Hooks { get; init; }
            public required ISimilarityLog SimLog { get; init; }
            public required IAddPipeline Pipeline { get; init; }
        }

        /// <summary>
        /// Build all app services around a chosen workspace.
        /// </summary>
        internal static AppServices Build(IWorkSpaceService ws)
        {
            // Core services
            var storage = new FileStorageService(ws);
            var hasher = new HashingService();

            var content = new CompositeContentExtractor();
            var sim = new ContentAwareSimilarityService(content);
            var metadata = new CompositeMetadataExtractor(content);

            var doiNorm = new LM.Infrastructure.Text.DoiNormalizer();
            var pmidNorm = new LM.Infrastructure.Text.PmidNormalizer();
            var pubmed = new PubMedClient();

            // Spoke handlers
            ISpokeHandler[] handlers = new ISpokeHandler[] { new ArticleSpokeHandler(), new DocumentSpokeHandler() };

            // Store uses the preferred ctor with normalizers
            var store = new HubSpokeStore(ws, hasher, handlers, doiNorm, pmidNorm, content);

            // Hooks
            var orchestrator = new HookOrchestrator(ws);

            // Similarity log
            var simlog = new LM.HubSpoke.Indexing.SimilarityLog(ws);

            // Pipeline (DI-style ctor)
            var pipeline = new LM.App.Wpf.ViewModels.AddPipeline(
                store, storage, hasher, sim, ws, metadata,
                pubmed, doiNorm,
                orchestrator,
                pmidNorm,          // <-- insert here
                simlog);

            return new AppServices
            {
                Workspaces = ws,
                Storage = storage,
                Hasher = hasher,
                ContentExtractor = content,
                Similarity = sim,
                Metadata = metadata,
                Doi = doiNorm,
                Pmid = pmidNorm,
                PubMed = pubmed,
                Store = store,
                Hooks = orchestrator,
                SimLog = simlog,
                Pipeline = pipeline
            };
        }
    }
}
