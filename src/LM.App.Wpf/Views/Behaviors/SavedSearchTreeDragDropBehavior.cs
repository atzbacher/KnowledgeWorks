using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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

            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;

            Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Detached from tree '{0}'.", AssociatedObject.Name);

        }

        private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = FindTreeViewItem(e.OriginalSource as System.Windows.DependencyObject);
            if (item?.DataContext is SavedSearchNodeViewModel node)
            {
                _dragStart = e.GetPosition(AssociatedObject);
                _dragSource = node;
                Trace.TraceInformation("SavedSearchTreeDragDropBehavior: Potential drag start from node '{0}'.", node.Name);

            }
            else
            {
                _dragStart = null;
                _dragSource = null;
                Trace.WriteLine("SavedSearchTreeDragDropBehavior: Mouse down outside draggable node.");

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
            tree = AssociatedObject.DataContext as SavedSearchTreeViewModel;
            return tree is not null;
        }

        // Replace the TryGetDropInfo method in SavedSearchTreeDragDropBehavior.cs
        // This implementation uses actual mouse position to determine drop behavior

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
                // Only apply position-based logic for folder-on-folder drops
                if (source is SavedSearchFolderViewModel)
                {
                    var dropPosition = GetDropPosition(item);

                    if (dropPosition == DropPosition.Before)
                    {
                        // Drop BEFORE this folder - insert as sibling before it
                        targetFolder = folder.Parent ?? tree.Root;
                        insertIndex = targetFolder.Children.IndexOf(folder);
                        return true;
                    }
                    else if (dropPosition == DropPosition.After)
                    {
                        // Drop AFTER this folder - insert as sibling after it
                        targetFolder = folder.Parent ?? tree.Root;
                        insertIndex = targetFolder.Children.IndexOf(folder) + 1;
                        return true;
                    }
                    else // DropPosition.Into
                    {
                        // Drop INTO this folder - make it a child
                        targetFolder = folder;
                        insertIndex = folder.Children.Count;
                        return true;
                    }
                }
                else
                {
                    // Preset dropped on folder - always make it a child
                    targetFolder = folder;
                    insertIndex = folder.Children.Count;
                    return true;
                }
            }

            if (item?.DataContext is SavedSearchPresetViewModel preset)
            {
                // Drop near preset - insert at same level
                targetFolder = preset.Parent ?? tree.Root;
                insertIndex = targetFolder.Children.IndexOf(preset);
                return true;
            }

            // Default - drop at root
            targetFolder = source.Parent ?? tree.Root;
            insertIndex = targetFolder.Children.Count;
            return true;
        }

        private enum DropPosition
        {
            Before,
            Into,
            After
        }

        private static DropPosition GetDropPosition(System.Windows.Controls.TreeViewItem item)
        {
            if (item == null)
                return DropPosition.Into;

            try
            {
                // Get mouse position relative to the TreeViewItem
                var mousePosition = System.Windows.Input.Mouse.GetPosition(item);

                // Get the actual rendered height of the item
                var actualHeight = item.ActualHeight;

                if (actualHeight <= 0)
                    return DropPosition.Into;

                // Define drop zones based on percentage of height:
                // Top 30% = Before (sibling above)
                // Middle 40% = Into (child)
                // Bottom 30% = After (sibling below)
                var topThreshold = actualHeight * 0.30;
                var bottomThreshold = actualHeight * 0.70;

                if (mousePosition.Y < topThreshold)
                {
                    System.Diagnostics.Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Drop position: BEFORE (Y={mousePosition.Y:F2}, threshold={topThreshold:F2})");
                    return DropPosition.Before;
                }
                else if (mousePosition.Y > bottomThreshold)
                {
                    System.Diagnostics.Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Drop position: AFTER (Y={mousePosition.Y:F2}, threshold={bottomThreshold:F2})");
                    return DropPosition.After;
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Drop position: INTO (Y={mousePosition.Y:F2})");
                    return DropPosition.Into;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Error determining drop position: {ex.Message}");
                return DropPosition.Into;
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
