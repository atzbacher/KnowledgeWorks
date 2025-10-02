using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.ViewModels.Library.Collections;
using LM.App.Wpf.ViewModels.Library.LitSearch;
using LM.App.Wpf.ViewModels.Library.SavedSearches;
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
            CollectionsTree.Loaded += OnCollectionsTreeLoaded;
        }

        private void OnSavedSearchTreeLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TreeView treeView)
            {
                Trace.TraceWarning("LibraryView: Saved search tree loaded with unexpected sender instance.");
                return;
            }

            treeView.Loaded -= OnSavedSearchTreeLoaded;

            BehaviorCollection behaviors = Interaction.GetBehaviors(treeView);
            if (behaviors.OfType<SavedSearchTreeDragDropBehavior>().Any())
            {
                Trace.TraceInformation("LibraryView: Saved search drag/drop behavior already attached.");
                return;
            }

            var dragDropBehavior = new SavedSearchTreeDragDropBehavior();
            behaviors.Add(dragDropBehavior);
            Trace.TraceInformation("LibraryView: Attached saved search drag/drop behavior to tree view.");
        }

        private void OnCollectionsTreeLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TreeView treeView)
            {
                Trace.TraceWarning("LibraryView: Collections tree loaded with unexpected sender instance.");
                return;
            }

            treeView.Loaded -= OnCollectionsTreeLoaded;

            BehaviorCollection behaviors = Interaction.GetBehaviors(treeView);
            if (behaviors.OfType<CollectionTreeDragDropBehavior>().Any())
            {
                Trace.TraceInformation("LibraryView: Collection drag/drop behavior already attached.");
                return;
            }

            var dragDropBehavior = new CollectionTreeDragDropBehavior();
            behaviors.Add(dragDropBehavior);
            Trace.TraceInformation("LibraryView: Attached collection drag/drop behavior to tree view.");
        }

        private async void OnCollectionSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (e.NewValue is LibraryCollectionFolderViewModel collection)
            {
                // Load entries in this collection
                await vm.LoadCollectionEntriesAsync(collection.Id).ConfigureAwait(false);
            }
        }

        private async void OnSavedSearchSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (e.NewValue is SavedSearchPresetViewModel preset)
            {
                // Apply the saved search
                await vm.Filters.ApplyPresetAsync(preset.Summary).ConfigureAwait(false);
            }
        }

        private async void OnLitSearchSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
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

        private async void OnLitSearchTreeViewDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (sender is System.Windows.Controls.TreeView treeView && treeView.SelectedItem is LitSearchNodeViewModel node &&
                node.NavigationNode is LibraryNavigationNodeViewModel navigation &&
                navigation.Kind == LibraryNavigationNodeKind.LitSearchRun)
            {
                await vm.HandleNavigationSelectionAsync(navigation).ConfigureAwait(false);
                e.Handled = true;
            }
        }

        private async void OnTagClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (sender is not FrameworkElement element || element.DataContext is not string tag)
            {
                return;
            }

            // Add the tag to filters and search
            vm.Filters.SelectedTags.Clear();
            vm.Filters.SelectedTags.Add(tag);

            if (vm.SearchCommand.CanExecute(null))
            {
                await Task.Run(() => vm.SearchCommand.Execute(null)).ConfigureAwait(false);
            }

            e.Handled = true;
        }

        private void OnFullTextToggleChanged(object sender, RoutedEventArgs e)
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