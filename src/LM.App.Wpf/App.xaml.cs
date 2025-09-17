#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;
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
            var libraryVm = new LibraryViewModel(services.Store, ws);
            var addVm = new AddViewModel(services.Pipeline);
            var searchVm = new SearchViewModel(services.Store, services.Storage, ws);

            // Bind
            if (shell.LibraryViewControl != null) shell.LibraryViewControl.DataContext = libraryVm;
            if (shell.AddViewControl != null) shell.AddViewControl.DataContext = addVm;
            if (shell.SearchViewControl != null) shell.SearchViewControl.DataContext = searchVm;

        }
    }
}
