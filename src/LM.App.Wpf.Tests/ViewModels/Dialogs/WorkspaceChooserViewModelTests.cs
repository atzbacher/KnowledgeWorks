using System;
using System.IO;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.ViewModels.Review;
using Xunit;

namespace LM.App.Wpf.Tests.ViewModels.Dialogs
{
    public sealed class WorkspaceChooserViewModelTests
    {
        [Fact]
        public void BrowseTrainingDataCommand_Updates_Path_From_Dialog()
        {
            using var temp = TempFile.Create(".traineddata");
            var dialog = new FakeDialogService(openFiles: new[] { temp.Path });
            var viewModel = new WorkspaceChooserViewModel(dialog);

            viewModel.BrowseTrainingDataCommand.Execute(null);

            Assert.Equal(temp.Path, viewModel.TessTrainingDataPath);
        }

        [Fact]
        public void Confirm_Copies_Training_Data_Into_Workspace()
        {
            using var workspace = TempDirectory.Create();
            using var training = TempFile.Create(".traineddata");
            File.WriteAllText(training.Path, "sample");

            var viewModel = new WorkspaceChooserViewModel(new FakeDialogService());
            viewModel.WorkspacePath = workspace.Path;
            viewModel.TessTrainingDataPath = training.Path;
            viewModel.RequireExistingDirectory = false;

            bool? dialogResult = null;
            viewModel.CloseRequested += (_, args) => dialogResult = args.DialogResult;

            viewModel.ConfirmCommand.Execute(null);

            var expected = Path.Combine(workspace.Path, ".knowledgeworks", "tessdata", Path.GetFileName(training.Path));

            Assert.True(File.Exists(expected));
            Assert.Equal("sample", File.ReadAllText(expected));
            Assert.Equal(Path.GetFullPath(workspace.Path), viewModel.SelectedWorkspacePath);
            Assert.Equal(expected, viewModel.SelectedTessTrainingDataPath);
            Assert.True(dialogResult);
        }

        private sealed class FakeDialogService : IDialogService
        {
            private readonly string[]? _openFiles;
            private readonly string? _folderResult;

            public FakeDialogService(string[]? openFiles = null, string? folderResult = null)
            {
                _openFiles = openFiles;
                _folderResult = folderResult;
            }

            public string[]? ShowOpenFileDialog(FilePickerOptions options)
            {
                return _openFiles;
            }

            public string? ShowFolderBrowserDialog(FolderPickerOptions options)
            {
                return _folderResult;
            }

            public bool? ShowStagingEditor(StagingListViewModel stagingList)
            {
                throw new NotSupportedException();
            }

            public bool? ShowProjectEditor(ProjectEditorViewModel editor)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            private TempDirectory(string path)
            {
                Path = path;
            }

            public static TempDirectory Create()
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw_ws_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                return new TempDirectory(path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }

        private sealed class TempFile : IDisposable
        {
            public string Path { get; }

            private TempFile(string path)
            {
                Path = path;
            }

            public static TempFile Create(string extension)
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw_tf_" + Guid.NewGuid().ToString("N") + extension);
                File.WriteAllText(path, string.Empty);
                return new TempFile(path);
            }

            public void Dispose()
            {
                try
                {
                    if (File.Exists(Path))
                    {
                        File.Delete(Path);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
