using System;

namespace LM.App.Wpf.Views
{
    public partial class ShellWindow : System.Windows.Window
    {
        public event EventHandler? NewWorkspaceRequested;
        public event EventHandler? LoadWorkspaceRequested;

        public ShellWindow()
        {
            InitializeComponent();
        }

        private void OnNewWorkspaceClick(object sender, System.Windows.RoutedEventArgs e)
        {
            NewWorkspaceRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnLoadWorkspaceClick(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadWorkspaceRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
