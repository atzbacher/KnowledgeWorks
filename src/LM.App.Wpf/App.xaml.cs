#nullable enable
using System;
using System.IO;
using System.Linq;
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
        private ShellWindow? _shell;
        private WorkspacePreferenceStore? _workspacePreferences;
        private bool _isInitializingWorkspace;

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
            _shell = shell;
            shell.NewWorkspaceRequested += OnNewWorkspaceRequested;
            shell.LoadWorkspaceRequested += OnLoadWorkspaceRequested;
            this.MainWindow = shell;
            shell.Show();

            _workspacePreferences = new WorkspacePreferenceStore();
            var lastWorkspace = _workspacePreferences.TryGetLastWorkspacePath();
            var initialized = false;

            if (!string.IsNullOrWhiteSpace(lastWorkspace))
            {
                initialized = await LoadWorkspaceAsync(lastWorkspace);
            }

            if (!initialized)
            {
                var selectedWorkspacePath = ShowWorkspaceDialog("Select or create workspace", requireExistingDirectory: false, initialPath: lastWorkspace);
                if (string.IsNullOrWhiteSpace(selectedWorkspacePath))
                {
                    shell.Close();
                    Shutdown();
                    return;
                }

                initialized = await LoadWorkspaceAsync(selectedWorkspacePath);
                if (!initialized)
                {
                    shell.Close();
                    Shutdown();
                    return;
                }
            }
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            if (_shell is not null)
            {
                _shell.NewWorkspaceRequested -= OnNewWorkspaceRequested;
                _shell.LoadWorkspaceRequested -= OnLoadWorkspaceRequested;
            }
            _addViewModel?.Dispose();
            _host?.Dispose();
            base.OnExit(e);
        }

        private async void OnNewWorkspaceRequested(object? sender, EventArgs e)
        {
            var selected = ShowWorkspaceDialog("Create workspace", requireExistingDirectory: false, initialPath: null);
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            await LoadWorkspaceAsync(selected);
        }

        private async void OnLoadWorkspaceRequested(object? sender, EventArgs e)
        {
            var initial = _workspacePreferences?.TryGetLastWorkspacePath();
            var selected = ShowWorkspaceDialog("Load workspace", requireExistingDirectory: true, initialPath: initial);
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            await LoadWorkspaceAsync(selected);
        }

        private string? ShowWorkspaceDialog(string title, bool requireExistingDirectory, string? initialPath)
        {
            using var services = new ServiceCollection()
                .AddSingleton<IDialogService, WpfDialogService>()
                .AddTransient<WorkspaceChooserViewModel>()
                .AddTransient<WorkspaceChooser>()
                .BuildServiceProvider();

            var chooser = services.GetRequiredService<WorkspaceChooser>();
            if (_shell is not null)
            {
                chooser.Owner = _shell;
            }

            chooser.Configure(viewModel =>
            {
                viewModel.Title = title;
                viewModel.RequireExistingDirectory = requireExistingDirectory;
                viewModel.WorkspacePath = initialPath ?? string.Empty;
                viewModel.EnableDebugDump = LM.App.Wpf.Diagnostics.DebugFlags.DumpStagingJson;

                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                {
                    var tessRoot = Path.Combine(initialPath, ".knowledgeworks", "tessdata");
                    if (Directory.Exists(tessRoot))
                    {
                        var existing = Directory.EnumerateFiles(tessRoot, "*.traineddata").FirstOrDefault()
                                       ?? Directory.EnumerateFiles(tessRoot).FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(existing))
                        {
                            viewModel.TessTrainingDataPath = existing;
                        }
                    }
                }
            });

            var ok = chooser.ShowDialog();
            return ok == true ? chooser.SelectedWorkspacePath : null;
        }

        private async Task<bool> LoadWorkspaceAsync(string workspacePath)
        {
            if (_shell is null)
            {
                return false;
            }

            if (_isInitializingWorkspace)
            {
                return false;
            }

            _isInitializingWorkspace = true;
            var previousCursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            _shell.IsEnabled = false;

            AppHost? newHost = null;
            AddViewModel? newAddViewModel = null;

            try
            {
                var workspaceService = new WorkspaceService();
                await workspaceService.EnsureWorkspaceAsync(workspacePath);

                newHost = AppHostBuilder.Create()
                                        .AddModule(new CoreModule(workspaceService))
                                        .AddModule(new AddModule())
                                        .AddModule(new LibraryModule())
                                        .AddModule(new ReviewModule())
                                        .AddModule(new SearchModule())
                                        .Build();

                var store = newHost.GetRequiredService<IEntryStore>();
                await store.InitializeAsync();

                var libraryVm = newHost.GetRequiredService<LibraryViewModel>();
                newAddViewModel = newHost.GetRequiredService<AddViewModel>();
                await newAddViewModel.InitializeAsync();
                var searchVm = newHost.GetRequiredService<SearchViewModel>();
                var reviewVm = newHost.GetRequiredService<ReviewViewModel>();

                AttachViewModels(libraryVm, newAddViewModel, searchVm, reviewVm);

                var previousHost = _host;
                var previousAdd = _addViewModel;

                _host = newHost;
                _addViewModel = newAddViewModel;

                _workspacePreferences?.SetLastWorkspacePath(workspaceService.GetWorkspaceRoot());

                previousAdd?.Dispose();
                previousHost?.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                newAddViewModel?.Dispose();
                newHost?.Dispose();
                System.Windows.MessageBox.Show($"Failed to open workspace:{Environment.NewLine}{ex.Message}",
                                               "Workspace",
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Error);
                return false;
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = previousCursor;
                _shell.IsEnabled = true;
                _isInitializingWorkspace = false;
            }
        }

        private void AttachViewModels(LibraryViewModel libraryVm,
                                      AddViewModel addVm,
                                      SearchViewModel searchVm,
                                      ReviewViewModel reviewVm)
        {
            if (_shell is null)
            {
                return;
            }

            if (_shell.FindName("LibraryViewControl") is LibraryView libraryView)
            {
                libraryView.DataContext = libraryVm;
            }

            if (_shell.FindName("AddViewControl") is AddView addView)
            {
                addView.DataContext = addVm;
            }

            if (_shell.FindName("SearchViewControl") is SearchView searchView)
            {
                searchView.DataContext = searchVm;
            }

            if (_shell.FindName("ReviewViewControl") is ReviewView reviewView)
            {
                reviewView.DataContext = reviewVm;
            }
        }
    }
}
