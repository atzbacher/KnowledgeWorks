using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.ViewModels.Library.SavedSearches;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    public sealed class SavedSearchTreeDragDropBehavior : Behavior<System.Windows.Controls.TreeView>
    {
        private System.Windows.Point? _dragStart;
        private SavedSearchNodeViewModel? _dragSource;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is null)
            {
                Trace.TraceWarning("SavedSearchTreeDragDropBehavior: Attached with null AssociatedObject.");
                return;
            }

            AssociatedObject.AllowDrop = true;
            Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Enabled AllowDrop on tree '{0}'.", AssociatedObject.Name);


            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;

            Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Attached to tree '{0}'.", AssociatedObject.Name);

        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject is null)
            {
                Trace.TraceWarning("SavedSearchTreeDragDropBehavior: Detaching with null AssociatedObject.");
                return;
            }

            AssociatedObject.AllowDrop = false;
            Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Disabled AllowDrop on tree '{0}'.", AssociatedObject.Name);

            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;

            Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Detached from tree '{0}'.", AssociatedObject.Name);

        }

        private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = FindTreeViewItem(e.OriginalSource as System.Windows.DependencyObject);
            if (item?.DataContext is SavedSearchNodeViewModel node && node.IsDraggable)
            {
                _dragStart = e.GetPosition(AssociatedObject);
                _dragSource = node;
                Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Potential drag start from node '{0}'.", node.Name);
            }
            else
            {
                _dragStart = null;
                _dragSource = null;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: Mouse down on non-draggable node or outside draggable area.");
            }
        }

        private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStart is null || _dragSource is null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                return;
            }

            var currentPosition = e.GetPosition(AssociatedObject);
            if (Math.Abs(currentPosition.X - _dragStart.Value.X) < System.Windows.SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _dragStart.Value.Y) < System.Windows.SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Initiating drag for node '{0}'.", _dragSource.Name);


            var data = new System.Windows.DataObject(typeof(SavedSearchNodeViewModel), _dragSource);
            Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Initiating drag for '{_dragSource.Id}'.");
            System.Windows.DragDrop.DoDragDrop(AssociatedObject, data, System.Windows.DragDropEffects.Move);
            _dragStart = null;
            _dragSource = null;
        }

        private void OnDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!TryGetDragSource(e, out var source) || !TryGetTree(out var tree))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: DragOver rejected - no source or tree.");

                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as System.Windows.DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: DragOver rejected - invalid drop target.");

                return;
            }

            if (source is SavedSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: DragOver rejected - ancestor detected.");

                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
            Trace.WriteLine($"SavedSearchTreeDragDropBehavior: DragOver accepted for target folder '{targetFolder.Name}' at index {insertIndex}.");

        }

        private async void OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (!TryGetDragSource(e, out var source) || !TryGetTree(out var tree))
            {
                e.Handled = true;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: Drop ignored - no source or tree context.");

                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as System.Windows.DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Handled = true;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: Drop ignored - invalid drop target.");

                return;
            }

            if (source is SavedSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
            {
                e.Handled = true;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: Drop ignored - ancestor detected.");

                return;
            }

            if (source.Parent == targetFolder)
            {
                var currentIndex = targetFolder.Children.IndexOf(source);
                if (currentIndex < insertIndex)
                {
                    insertIndex--;
                }
            }

            var request = new SavedSearchDragDropRequest
            {
                Source = source,
                TargetFolder = targetFolder,
                InsertIndex = Math.Max(0, insertIndex)
            };

            if (tree.MoveCommand.CanExecute(request))
            {
                Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Executing move for '{0}' into folder '{1}' at index {2}.", source.Name, targetFolder.Name, request.InsertIndex);

                await tree.MoveCommand.ExecuteAsync(request);
            }
            else
            {
                Trace.TraceWarning("SavedSearchTreeDragDropBehavior: Move command rejected for '{0}'.", source.Name);
            }


            e.Handled = true;
        }

        private static bool TryGetDragSource(System.Windows.DragEventArgs e, [NotNullWhen(true)] out SavedSearchNodeViewModel? source)
        {
            source = e.Data.GetData(typeof(SavedSearchNodeViewModel)) as SavedSearchNodeViewModel;
            if (source is null)
            {
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: No saved search node found in drag data.");
                return false;
            }

            Trace.WriteLine($"SavedSearchTreeDragDropBehavior: Drag source resolved to node '{source.Name}'.");

            return source is not null;
        }

        private bool TryGetTree([NotNullWhen(true)] out SavedSearchTreeViewModel? tree)
        {
            tree = null;

            var associatedObject = AssociatedObject;
            if (associatedObject is null)
            {
                Trace.TraceWarning("SavedSearchTreeDragDropBehavior: TryGetTree invoked with null AssociatedObject.");
                return false;
            }

            if (associatedObject.DataContext is SavedSearchTreeViewModel directTree)
            {
                tree = directTree;
                Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Resolved tree from direct DataContext binding.");
                return true;
            }

            if (associatedObject.DataContext is LibraryFiltersViewModel filters)
            {
                tree = filters.SavedSearches;
                if (tree is not null)
                {
                    Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Resolved tree from LibraryFiltersViewModel.");
                    return true;
                }
            }

            var contextTypeName = associatedObject.DataContext?.GetType().FullName ?? "<null>";
            Trace.TraceWarning(
                "SavedSearchTreeDragDropBehavior: Unable to resolve SavedSearchTreeViewModel. DataContext type: '{0}'.",
                contextTypeName);

            return false;
        }



        private static bool TryGetDropInfo(System.Windows.DependencyObject? sourceElement,
                                           SavedSearchTreeViewModel tree,
                                           SavedSearchNodeViewModel source,
                                           out SavedSearchFolderViewModel targetFolder,
                                           out int insertIndex)
        {
            ArgumentNullException.ThrowIfNull(source);

            var item = FindTreeViewItem(sourceElement);

            if (item?.DataContext is SavedSearchFolderViewModel folder)
            {
                // For folder-on-folder drops
                if (source is SavedSearchFolderViewModel sourceFolder)
                {
                    // Prevent dropping a folder into itself or its descendants
                    if (IsAncestor(sourceFolder, folder))
                    {
                        targetFolder = tree.Root;
                        insertIndex = 0;
                        return false;
                    }

                    var dropPosition = GetDropPosition(item);

                    if (dropPosition == DropPosition.Center)
                    {
                        // Drop INTO this folder (nest as child)
                        targetFolder = folder;
                        insertIndex = folder.Children.Count; // Add at end
                        return true;
                    }
                    else if (dropPosition == DropPosition.Before)
                    {
                        // Drop BEFORE this folder (insert as sibling before it)
                        targetFolder = folder.Parent ?? tree.Root;
                        insertIndex = targetFolder.Children.IndexOf(folder);
                        return true;
                    }
                    else if (dropPosition == DropPosition.After)
                    {
                        // Drop AFTER this folder (insert as sibling after it)
                        targetFolder = folder.Parent ?? tree.Root;
                        insertIndex = targetFolder.Children.IndexOf(folder) + 1;
                        return true;
                    }
                }

                // For preset drops on folders, always nest inside
                targetFolder = folder;
                insertIndex = folder.Children.OfType<SavedSearchPresetViewModel>().Count();
                return true;
            }

            // If dropping on preset, insert after it
            if (item?.DataContext is SavedSearchPresetViewModel preset)
            {
                targetFolder = preset.Parent ?? tree.Root;
                insertIndex = targetFolder.Children.IndexOf(preset) + 1;
                return true;
            }

            // Default: add to root
            targetFolder = tree.Root;
            insertIndex = targetFolder.Children.Count;
            return true;
        }

        private enum DropPosition
        {
            Before,
            Center,
            After
        }

        private static DropPosition GetDropPosition(System.Windows.Controls.TreeViewItem item)
        {
            var mousePos = System.Windows.Input.Mouse.GetPosition(item);
            var height = item.ActualHeight;

            const double edgeThreshold = 0.25; // 25% from top/bottom is edge zone

            if (mousePos.Y < height * edgeThreshold)
            {
                return DropPosition.Before;
            }
            else if (mousePos.Y > height * (1 - edgeThreshold))
            {
                return DropPosition.After;
            }
            else
            {
                return DropPosition.Center;
            }
        
        }

        private static bool IsAncestor(SavedSearchFolderViewModel ancestor, SavedSearchFolderViewModel candidate)
        {
            var current = candidate;
            while (current is not null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static System.Windows.Controls.TreeViewItem? FindTreeViewItem(System.Windows.DependencyObject? current)
        {
            while (current is not null && current is not System.Windows.Controls.TreeViewItem)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return current as System.Windows.Controls.TreeViewItem;
        }
    }
}
