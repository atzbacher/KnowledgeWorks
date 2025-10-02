using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Library;

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

        private void OnResultsSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (sender is not System.Windows.Controls.DataGrid grid)
            {
                return;
            }

            var selectedItems = grid.SelectedItems;
            var command = vm.Results.HandleSelectionChangedCommand;
            if (command is not null)
            {
                if (command.CanExecute(selectedItems))
                {
                    command.Execute(selectedItems);
                }
                else if (command.CanExecute(null))
                {
                    command.Execute(null);
                }
            }

            System.Diagnostics.Trace.WriteLine($"[LibraryView] Selection changed → {selectedItems?.Count ?? 0} rows");
        }

        private void OnResultsDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (sender is not System.Windows.Controls.DataGrid grid)
            {
                return;
            }

            if (grid.SelectedItem is LibrarySearchResult result && vm.OpenEntryCommand.CanExecute(result))
            {
                System.Diagnostics.Trace.WriteLine($"[LibraryView] Double-click open entry → {result.Entry?.Title ?? "<unknown>"}");
                vm.OpenEntryCommand.Execute(result);
            }
        }
    }
}
