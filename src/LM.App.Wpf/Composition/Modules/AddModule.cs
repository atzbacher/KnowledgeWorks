using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.Views;
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
                sp.GetRequiredService<IDataExtractionPreprocessor>(),
                sp.GetRequiredService<ISimilarityLog>()));

            services.AddSingleton<WatchedFolderScanner>(sp => new WatchedFolderScanner(sp.GetRequiredService<IAddPipeline>()));
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddTransient<StagingEditorViewModel>();
            services.AddTransient<StagingEditorWindow>();
            services.AddSingleton<StagingListViewModel>(sp => new StagingListViewModel(sp.GetRequiredService<IAddPipeline>()));
            services.AddSingleton<WatchedFoldersViewModel>(sp => new WatchedFoldersViewModel(
                sp.GetRequiredService<StagingListViewModel>(),
                sp.GetRequiredService<WatchedFolderScanner>(),
                sp.GetRequiredService<IWatchedFolderSettingsStore>(),
                sp.GetRequiredService<IDialogService>()));

            services.AddSingleton(sp => new AddViewModel(
                sp.GetRequiredService<IAddPipeline>(),
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<WatchedFolderScanner>(),
                sp.GetRequiredService<IWatchedFolderSettingsStore>(),
                sp.GetRequiredService<StagingListViewModel>(),
                sp.GetRequiredService<WatchedFoldersViewModel>(),
                sp.GetRequiredService<IDialogService>()));
        }
    }
}
