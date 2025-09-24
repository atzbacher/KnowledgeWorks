using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.Views;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition.Modules
{
    internal sealed class LibraryModule : IAppModule
    {
        public void ConfigureServices(HostApplicationBuilder builder)
        {
            var services = builder.Services;

            services.AddSingleton<LibraryFilterPresetStore>();
            services.AddSingleton<ILibraryPresetPrompt, LibraryPresetPrompt>();
            services.AddTransient<LibraryPresetSaveDialogViewModel>();
            services.AddTransient<LibraryPresetSaveDialog>();
            services.AddTransient<LibraryPresetPickerDialogViewModel>();
            services.AddTransient<LibraryPresetPickerDialog>();
            services.AddSingleton<ILibraryEntryEditor>(sp => new WorkspaceEntryEditor(sp.GetRequiredService<IWorkSpaceService>()));
            services.AddSingleton<ILibraryDocumentService>(sp => new LibraryDocumentService(sp.GetRequiredService<IWorkSpaceService>()));

            services.AddSingleton(sp => new LibraryFiltersViewModel(
                sp.GetRequiredService<LibraryFilterPresetStore>(),
                sp.GetRequiredService<ILibraryPresetPrompt>()));

            services.AddSingleton(sp => new LibraryResultsViewModel(
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IFileStorageRepository>(),
                sp.GetRequiredService<ILibraryEntryEditor>(),
                sp.GetRequiredService<ILibraryDocumentService>()));

            services.AddSingleton(sp => new LibraryViewModel(
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IFullTextSearchService>(),
                sp.GetRequiredService<LibraryFiltersViewModel>(),
                sp.GetRequiredService<LibraryResultsViewModel>()));
        }
    }
}
