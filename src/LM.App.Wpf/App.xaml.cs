#nullable enable
using System;
using System.Threading.Tasks;
using LM.App.Wpf.Application;
using LM.App.Wpf.Composition.Modules;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.ViewModels.Review;
using LM.App.Wpf.Views;
using LM.App.Wpf.Views.Review;
using LM.Core.Abstractions;
using LM.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf
{
    public partial class App : System.Windows.Application
    {
        private AppHost? _host;
        private AddViewModel? _addViewModel;

        protected override async void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);
            // Show unhandled exceptions (helps catch XamlParseException on window init)
            this.DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show(args.Exception.ToString(), "Dispatcher Unhandled Exception");
                // leave args.Handled = false so debugger still breaks
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    System.Windows.MessageBox.Show(ex.ToString(), "AppDomain Unhandled Exception");
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                System.Windows.MessageBox.Show(args.Exception.ToString(), "TaskScheduler Unobserved Exception");
                args.SetObserved();
            };

            var shell = new ShellWindow();
            this.MainWindow = shell;
            shell.Show();

            using var startupServices = new ServiceCollection()
                .AddSingleton<IDialogService, WpfDialogService>()
                .AddTransient<WorkspaceChooserViewModel>()
                .AddTransient<WorkspaceChooser>()
                .BuildServiceProvider();

            var chooser = startupServices.GetRequiredService<WorkspaceChooser>();
            chooser.Owner = shell;
            var ok = chooser.ShowDialog();
            var selectedWorkspacePath = chooser.SelectedWorkspacePath;
            if (ok != true || string.IsNullOrWhiteSpace(selectedWorkspacePath))
            {
                shell.Close();
                Shutdown();
                return;
            }
            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(selectedWorkspacePath);

            var host = AppHostBuilder.Create()
                                     .AddModule(new CoreModule(ws))
                                     .AddModule(new AddModule())
                                     .AddModule(new LibraryModule())
                                     .AddModule(new ReviewModule())
                                     .AddModule(new SearchModule())
                                     .Build();
            _host = host;

            var store = host.GetRequiredService<IEntryStore>();
            await store.InitializeAsync();

            var libraryVm = host.GetRequiredService<LibraryViewModel>();
            var addVm = host.GetRequiredService<AddViewModel>();
            await addVm.InitializeAsync();
            _addViewModel = addVm;
            var searchVm = host.GetRequiredService<SearchViewModel>();
            var reviewVm = host.GetRequiredService<ReviewViewModel>();

            // Bind – resolve the views by name because the generated fields are not
            // available when building from the command line (designer-only feature).
            if (shell.FindName("LibraryViewControl") is LibraryView libraryView)
                libraryView.DataContext = libraryVm;

            if (shell.FindName("AddViewControl") is AddView addView)
                addView.DataContext = addVm;

            if (shell.FindName("SearchViewControl") is SearchView searchView)
                searchView.DataContext = searchVm;

            if (shell.FindName("ReviewViewControl") is ReviewView reviewView)
                reviewView.DataContext = reviewVm;

        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            _addViewModel?.Dispose();
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
