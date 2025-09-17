namespace LM.App.Wpf.Views
{
    public partial class AddView : System.Windows.Controls.UserControl
    {
        public AddView() { InitializeComponent(); }

        private void OnReviewStaged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not LM.App.Wpf.ViewModels.AddViewModel vm) return;
            var win = new StagingEditorWindow(vm) { Owner = System.Windows.Window.GetWindow(this) };
            win.ShowDialog();
        }

        private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OnReviewStaged(sender, e);
    }
}
