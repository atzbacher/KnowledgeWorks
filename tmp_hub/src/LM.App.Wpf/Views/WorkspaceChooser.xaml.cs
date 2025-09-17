using LM.App.Wpf.Diagnostics;

namespace LM.App.Wpf.Views
{
    public partial class WorkspaceChooser : System.Windows.Window
    {
        public string? SelectedWorkspacePath { get; private set; }
        public WorkspaceChooser() { InitializeComponent(); }

        private void OnBrowse(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the workspace folder (shared via OneDrive/SharePoint)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                PathBox.Text = dlg.SelectedPath;
        }

        private void OnOk(object sender, System.Windows.RoutedEventArgs e)
        {
            var path = PathBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("Please choose an existing folder.", "Workspace",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // NEW: set debug flag from checkbox (internal toggle)
            DebugFlags.DumpStagingJson = DebugDumpCheck.IsChecked == true;

            SelectedWorkspacePath = System.IO.Path.GetFullPath(path);
            DialogResult = true;
            Close();
        }
    }
}
