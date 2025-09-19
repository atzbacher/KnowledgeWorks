#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;
using LM.App.Wpf.Library;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.Views;
using LM.Core.Abstractions;
using LM.HubSpoke.Spokes;
using LM.HubSpoke.Abstractions;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Storage;
using LM.Infrastructure.Utils;
using LM.Infrastructure.Content;
using LM.Infrastructure.Metadata;
using LM.Infrastructure.Text;
using LM.Infrastructure.PubMed;
using LM.HubSpoke.Indexing;



using WpfMessageBox = System.Windows.MessageBox;

namespace LM.App.Wpf
{
    public partial class App : System.Windows.Application
    {
        private AddViewModel? _addViewModel;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Show unhandled exceptions (helps catch XamlParseException on window init)
            this.DispatcherUnhandledException += (s, args) =>
            {
                WpfMessageBox.Show(args.Exception.ToString(), "Dispatcher Unhandled Exception");
                // leave args.Handled = false so debugger still breaks
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    WpfMessageBox.Show(ex.ToString(), "AppDomain Unhandled Exception");
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                WpfMessageBox.Show(args.Exception.ToString(), "TaskScheduler Unobserved Exception");
                args.SetObserved();
            };

            var shell = new ShellWindow();
            this.MainWindow = shell;
            shell.Show();

            var ws = new WorkspaceService();
            var chooser = new WorkspaceChooser { Owner = shell };
            var ok = chooser.ShowDialog();
            if (ok != true || string.IsNullOrWhiteSpace(chooser.SelectedWorkspacePath))
            {
                shell.Close();
                Shutdown();
                return;
            }
            await ws.EnsureWorkspaceAsync(chooser.SelectedWorkspacePath);

            // Build all services around the chosen workspace (centralized wiring)
            var services = LM.App.Wpf.Composition.ServiceConfig.Build(ws);

            // Initialize store
            await services.Store.InitializeAsync();

            // ViewModels
            var presetStore = new LibraryFilterPresetStore(ws);
            var presetPrompt = new LibraryPresetPrompt();
            var libraryVm = new LibraryViewModel(services.Store, ws, presetStore, presetPrompt);
            var addVm = new AddViewModel(services.Pipeline, ws, services.Scanner);
            await addVm.InitializeAsync();
            _addViewModel = addVm;
            var searchPrompt = new SearchSavePrompt();
            var searchVm = new SearchViewModel(services.Store, services.Storage, ws, searchPrompt);

            // Bind – resolve the views by name because the generated fields are not
            // available when building from the command line (designer-only feature).
            if (shell.FindName("LibraryViewControl") is LibraryView libraryView)
                libraryView.DataContext = libraryVm;

            if (shell.FindName("AddViewControl") is AddView addView)
                addView.DataContext = addVm;

            if (shell.FindName("SearchViewControl") is SearchView searchView)
                searchView.DataContext = searchVm;

        }

        protected override void OnExit(ExitEventArgs e)
        {
            _addViewModel?.Dispose();
            base.OnExit(e);
        }
    }
}
