using System.Diagnostics;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.Views.Behaviors;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views
{
    public partial class LibraryView : System.Windows.Controls.UserControl
    {
        public LibraryView()
        {
            InitializeComponent();
            SavedSearchTree.Loaded += OnSavedSearchTreeLoaded;
        }

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

        private void OnSavedSearchTreeLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TreeView treeView)
            {
                Trace.TraceWarning("LibraryView: Saved search tree loaded with unexpected sender instance.");
                return;
            }

            treeView.Loaded -= OnSavedSearchTreeLoaded;

            BehaviorCollection behaviors = Interaction.GetBehaviors(treeView);
            foreach (Behavior behavior in behaviors)
            {
                if (behavior is SavedSearchTreeDragDropBehavior)
                {
                    Trace.TraceInformation("LibraryView: Saved search drag/drop behavior already attached.");
                    return;
                }
            }

            var dragDropBehavior = new SavedSearchTreeDragDropBehavior();
            behaviors.Add(dragDropBehavior);
            Trace.TraceInformation("LibraryView: Attached saved search drag/drop behavior to tree view.");
        }

        private void OnFullTextToggleChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                Trace.TraceWarning("LibraryView: Full-text toggle changed without LibraryViewModel data context.");
                return;
            }

            if (vm.SearchCommand.CanExecute(null))
            {
                Trace.TraceInformation("LibraryView: Executing search after full-text toggle change.");
                vm.SearchCommand.Execute(null);
            }
            else
            {
                Trace.TraceInformation("LibraryView: Search command unavailable after full-text toggle change.");
            }
        }
    }
}
