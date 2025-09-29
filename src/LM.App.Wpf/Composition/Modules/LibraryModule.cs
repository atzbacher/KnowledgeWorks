using System;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.Views;
using LM.App.Wpf.Views.Library;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Repositories;
using LM.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LM.App.Wpf.Composition.Modules
{
    internal sealed class LibraryModule : IAppModule
    {
        public void ConfigureServices(HostApplicationBuilder builder)
        {
            var services = builder.Services;

            services.TryAddSingleton<IUserPreferencesStore, JsonUserPreferencesStore>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IFileExplorerService, FileExplorerService>();
            services.AddSingleton<LibraryFilterPresetStore>();
            services.AddSingleton<ILibraryPresetPrompt, LibraryPresetPrompt>();
            services.AddSingleton<ILibraryAnnotationRepository, JsonLibraryAnnotationRepository>();
            services.AddTransient<LibraryPresetSaveDialogViewModel>();
            services.AddTransient<LibraryPresetSaveDialog>();
            services.AddTransient<LibraryPresetPickerDialogViewModel>();
            services.AddTransient<LibraryPresetPickerDialog>();
            services.AddTransient<EntryEditorViewModel>();
            services.AddSingleton<ILibraryEntryEditor, LibraryEntryEditor>();
            services.AddTransient<PdfViewerViewModel>();
            services.AddTransient<PdfViewerWindow>();
            services.AddSingleton<IPdfViewerLauncher, PdfViewerLauncher>();
            services.AddSingleton<ILibraryDocumentService, LibraryDocumentService>();
            services.AddTransient<DataExtractionPlaygroundViewModel>();
            services.AddSingleton<LibraryDataExtractionLauncher>();
            services.AddSingleton<Func<Entry, CancellationToken, Task<bool>>>(sp =>
            {
                var launcher = sp.GetRequiredService<LibraryDataExtractionLauncher>();
                return (entry, token) => launcher.LaunchAsync(entry, token);
            });
            services.AddTransient<AttachmentMetadataDialogViewModel>();
            services.AddSingleton<IAttachmentMetadataPrompt, AttachmentMetadataPrompt>();

            services.AddSingleton(sp => new LibraryFiltersViewModel(
                sp.GetRequiredService<LibraryFilterPresetStore>(),
                sp.GetRequiredService<ILibraryPresetPrompt>(),
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IWorkSpaceService>()));

            services.AddSingleton(sp => new LibraryResultsViewModel(
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IFileStorageRepository>(),
                sp.GetRequiredService<ILibraryEntryEditor>(),
                sp.GetRequiredService<ILibraryDocumentService>(),
                sp.GetRequiredService<IAttachmentMetadataPrompt>(),
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<HookOrchestrator>()));

            services.AddSingleton(sp => new LibraryViewModel(
                sp.GetRequiredService<IEntryStore>(),
                sp.GetRequiredService<IFullTextSearchService>(),
                sp.GetRequiredService<LibraryFiltersViewModel>(),
                sp.GetRequiredService<LibraryResultsViewModel>(),
                sp.GetRequiredService<IWorkSpaceService>(),
                sp.GetRequiredService<IUserPreferencesStore>(),
                sp.GetRequiredService<IClipboardService>(),
                sp.GetRequiredService<IFileExplorerService>(),
                sp.GetRequiredService<ILibraryDocumentService>(),
                sp.GetRequiredService<Func<Entry, CancellationToken, Task<bool>>>()));
        }
    }
}
