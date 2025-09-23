using LM.App.Wpf.Common;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.Views;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition.Modules
{
    internal sealed class SearchModule : IAppModule
    {
        public void ConfigureServices(HostApplicationBuilder builder)
        {
            var services = builder.Services;

            services.AddSingleton<ISearchSavePrompt, SearchSavePrompt>();
            services.AddSingleton<ISearchHistoryStore>(sp => new JsonSearchHistoryStore(sp.GetRequiredService<IWorkSpaceService>()));
            services.AddSingleton<IUserPreferencesStore, JsonUserPreferencesStore>();

            services.AddSingleton(sp => new SearchViewModel(
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IFileStorageRepository>(),
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<ISearchSavePrompt>(),
                sp.GetRequiredService<ISearchHistoryStore>(),
                sp.GetRequiredService<IUserPreferencesStore>()));
        }
    }
}
