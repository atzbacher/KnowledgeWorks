using System;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.Entries;
using LM.HubSpoke.Indexing;
using LM.HubSpoke.Spokes;
using LM.Infrastructure.Content;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Metadata;
using LM.Infrastructure.PubMed;
using LM.Infrastructure.Settings;
using LM.Infrastructure.Storage;
using LM.Infrastructure.Text;
using LM.Infrastructure.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition.Modules
{
    internal sealed class CoreModule : IAppModule
    {
        private readonly IWorkSpaceService _workspace;

        public CoreModule(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public void ConfigureServices(HostApplicationBuilder builder)
        {
            var services = builder.Services;

            services.AddSingleton(_workspace);
            services.AddSingleton<IWorkSpaceService>(_ => _workspace);

            services.AddSingleton<IFileStorageRepository>(sp => new FileStorageService(sp.GetRequiredService<IWorkSpaceService>()));
            services.AddSingleton<IHasher, HashingService>();
            services.AddSingleton<IContentExtractor, CompositeContentExtractor>();
            services.AddSingleton<ISimilarityService>(sp => new ContentAwareSimilarityService(sp.GetRequiredService<IContentExtractor>()));
            services.AddSingleton<IMetadataExtractor>(sp => new CompositeMetadataExtractor(sp.GetRequiredService<IContentExtractor>()));
            services.AddSingleton<IDoiNormalizer, DoiNormalizer>();
            services.AddSingleton<IPmidNormalizer, PmidNormalizer>();
            services.AddSingleton<IPublicationLookup, PubMedClient>();

            services.AddSingleton<ISpokeHandler, ArticleSpokeHandler>();
            services.AddSingleton<ISpokeHandler, DocumentSpokeHandler>();
            services.AddSingleton<ISpokeHandler>(sp => new LitSearchSpokeHandler(sp.GetRequiredService<IWorkSpaceService>()));

            services.AddSingleton<HookOrchestrator>(sp => new HookOrchestrator(sp.GetRequiredService<IWorkSpaceService>()));
            services.AddSingleton<ISimilarityLog>(sp => new SimilarityLog(sp.GetRequiredService<IWorkSpaceService>()));

            services.AddSingleton<HubSpokeStore>(sp => new HubSpokeStore(
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<IHasher>(),
                sp.GetServices<ISpokeHandler>(),
                sp.GetRequiredService<IDoiNormalizer>(),
                sp.GetRequiredService<IPmidNormalizer>(),
                sp.GetRequiredService<IContentExtractor>()));

            services.AddSingleton<IEntryStore>(sp => sp.GetRequiredService<HubSpokeStore>());
            services.AddSingleton<IFullTextSearchService>(sp => sp.GetRequiredService<HubSpokeStore>().FullTextSearch);

            services.AddSingleton<IWatchedFolderSettingsStore>(sp => new JsonWatchedFolderSettingsStore(sp.GetRequiredService<IWorkSpaceService>()));
        }
    }
}
