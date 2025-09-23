#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LM.App.Wpf.Services;
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.HubSpoke.Abstractions;
using LM.Infrastructure.Hooks;

namespace LM.App.Wpf.ViewModels.Add
{
    public class AddViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public StagingListViewModel Staging { get; }
        public WatchedFoldersViewModel WatchedFolders { get; }

        public AddViewModel(IAddPipeline pipeline,
                            IDialogService dialogService,
                            IWatchedFolderScanner watchedFolderScanner,
                            IWatchedFolderConfigStore configStore)
        {
            if (pipeline is null) throw new ArgumentNullException(nameof(pipeline));
            if (dialogService is null) throw new ArgumentNullException(nameof(dialogService));
            if (watchedFolderScanner is null) throw new ArgumentNullException(nameof(watchedFolderScanner));
            if (configStore is null) throw new ArgumentNullException(nameof(configStore));

            Staging = new StagingListViewModel(pipeline, dialogService);
            WatchedFolders = new WatchedFoldersViewModel(configStore, watchedFolderScanner, dialogService, Staging);
        }

        public AddViewModel(IAddPipeline pipeline)
            : this(pipeline,
                   new NullDialogService(),
                   new FileSystemWatchedFolderScanner(),
                   new InMemoryWatchedFolderConfigStore())
        {
        }

        public AddViewModel(IEntryStore store,
                            IFileStorageRepository storage,
                            IHasher hasher,
                            ISimilarityService similarity,
                            IWorkSpaceService workspace,
                            IMetadataExtractor metadata,
                            IPublicationLookup publicationLookup,
                            IDoiNormalizer doiNormalizer,
                            HookOrchestrator orchestrator,
                            IPmidNormalizer pmidNormalizer,
                            ISimilarityLog? simLog = null,
                            IDialogService? dialogService = null,
                            IWatchedFolderScanner? watchedFolderScanner = null,
                            IWatchedFolderConfigStore? configStore = null)
            : this(new AddPipeline(store, storage, hasher, similarity, workspace, metadata,
                                   publicationLookup, doiNormalizer,
                                   orchestrator,
                                   pmidNormalizer,
                                   simLog),
                   dialogService ?? new NullDialogService(),
                   watchedFolderScanner ?? new FileSystemWatchedFolderScanner(),
                   configStore ?? new WatchedFolderConfigStore(workspace))
        {
        }

        public AddViewModel(IEntryStore store,
                            IFileStorageRepository storage,
                            IHasher hasher,
                            ISimilarityService similarity,
                            IWorkSpaceService workspace,
                            IMetadataExtractor metadata,
                            IPublicationLookup publicationLookup,
                            IDoiNormalizer doiNormalizer,
                            ISimilarityLog? simLog = null,
                            IDialogService? dialogService = null,
                            IWatchedFolderScanner? watchedFolderScanner = null,
                            IWatchedFolderConfigStore? configStore = null)
            : this(store, storage, hasher, similarity, workspace, metadata,
                   publicationLookup, doiNormalizer,
                   new HookOrchestrator(workspace),
                   new LM.Infrastructure.Text.PmidNormalizer(),
                   simLog,
                   dialogService,
                   watchedFolderScanner,
                   configStore)
        {
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

