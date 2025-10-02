using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.ViewModels.Library.LitSearch;

namespace LM.App.Wpf.Views
{
    public partial class LibraryView : System.Windows.Controls.UserControl
    {
        public LibraryView() => InitializeComponent();

        private async void OnNavigationSelected(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (e.NewValue is LibraryNavigationNodeViewModel node)
            {
                await vm.HandleNavigationSelectionAsync(node).ConfigureAwait(false);
            }
        }

        private async void OnLitSearchSelected(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (e.NewValue is LitSearchNodeViewModel node && node.NavigationNode is LibraryNavigationNodeViewModel navigation)
            {
                if (navigation.Kind != LibraryNavigationNodeKind.LitSearchRun)
                {
                    await vm.HandleNavigationSelectionAsync(navigation).ConfigureAwait(false);
                }
            }
        }

        private async void OnLitSearchTreeViewDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (sender is System.Windows.Controls.TreeView treeView && treeView.SelectedItem is LitSearchNodeViewModel node && node.NavigationNode is LibraryNavigationNodeViewModel navigation && navigation.Kind == LibraryNavigationNodeKind.LitSearchRun)
            {
                await vm.HandleNavigationSelectionAsync(navigation).ConfigureAwait(false);
                e.Handled = true;
            }
        }
    }
}
