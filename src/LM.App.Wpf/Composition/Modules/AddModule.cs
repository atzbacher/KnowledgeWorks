using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.HubSpoke.Abstractions;
using LM.Infrastructure.Hooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition.Modules
{
    internal sealed class AddModule : IAppModule
    {
        public void ConfigureServices(HostApplicationBuilder builder)
        {
            var services = builder.Services;

            services.AddSingleton<IAddPipeline>(sp => new AddPipeline(
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IFileStorageRepository>(),
                sp.GetRequiredService<IHasher>(),
                sp.GetRequiredService<ISimilarityService>(),
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<IMetadataExtractor>(),
                sp.GetRequiredService<IPublicationLookup>(),
                sp.GetRequiredService<IDoiNormalizer>(),
                sp.GetRequiredService<HookOrchestrator>(),
                sp.GetRequiredService<IPmidNormalizer>(),
                sp.GetRequiredService<ISimilarityLog>()));

            services.AddSingleton<WatchedFolderScanner>(sp => new WatchedFolderScanner(sp.GetRequiredService<IAddPipeline>()));

            services.AddSingleton(sp => new AddViewModel(
                sp.GetRequiredService<IAddPipeline>(),
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<WatchedFolderScanner>(),
                sp.GetRequiredService<IWatchedFolderSettingsStore>()));
        }
    }
}
