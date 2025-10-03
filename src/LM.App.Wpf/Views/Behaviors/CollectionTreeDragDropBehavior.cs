using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LM.App.Wpf.ViewModels.Library.Collections;
using Microsoft.Xaml.Behaviors;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace LM.App.Wpf.Views.Behaviors
{
    public sealed class CollectionTreeDragDropBehavior : Behavior<System.Windows.Controls.TreeView>
    {
        private System.Windows.Point? _dragStart;
        private LibraryCollectionFolderViewModel? _dragSource;

        protected override void OnAttached()
        {
            base.OnAttached();

            var treeView = AssociatedObject;
            if (treeView is null)
            {
                Trace.TraceWarning("CollectionTreeDragDropBehavior: OnAttached invoked without an associated tree view.");
                return;
            }

            treeView.AllowDrop = true;
            Trace.TraceInformation("CollectionTreeDragDropBehavior: Enabled AllowDrop on associated tree view '{0}'.", treeView.Name);
            treeView.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            treeView.PreviewMouseMove += OnPreviewMouseMove;
            treeView.DragOver += OnDragOver;
            treeView.Drop += OnDrop;
        }

        protected override void OnDetaching()
        {
            var treeView = AssociatedObject;
            if (treeView is null)
            {
                Trace.TraceWarning("CollectionTreeDragDropBehavior: OnDetaching invoked without an associated tree view.");
                base.OnDetaching();
                return;
            }

            treeView.AllowDrop = false;
            Trace.TraceInformation("CollectionTreeDragDropBehavior: Disabled AllowDrop on associated tree view '{0}'.", treeView.Name);
            treeView.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
            treeView.PreviewMouseMove -= OnPreviewMouseMove;
            treeView.DragOver -= OnDragOver;
            treeView.Drop -= OnDrop;
            base.OnDetaching();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            var element = e.OriginalSource as DependencyObject;
            var item = FindTreeViewItem(element);
            if (item?.DataContext is LibraryCollectionFolderViewModel node)
            {
                _dragStart = e.GetPosition(AssociatedObject);
                _dragSource = node;
                Trace.WriteLine($"CollectionTreeDragDropBehavior: Mouse down on node '{node.Name}'.");
            }
            else
            {
                _dragStart = null;
                _dragSource = null;
                Trace.WriteLine("CollectionTreeDragDropBehavior: Mouse down outside draggable node.");
            }
        }

        private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStart is null || _dragSource is null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPosition = e.GetPosition(AssociatedObject);
            if (Math.Abs(currentPosition.X - _dragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _dragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            Trace.TraceInformation("CollectionTreeDragDropBehavior: Initiating drag for node '{0}'.", _dragSource.Name);

            var data = new System.Windows.DataObject(typeof(LibraryCollectionFolderViewModel), _dragSource);
            Trace.WriteLine($"[CollectionTreeDragDropBehavior] Initiating drag for '{_dragSource.Id}'.");
            DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);
            _dragStart = null;
            _dragSource = null;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!TryGetDragSource(e, out var source) || !TryGetTree(out var tree))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                Trace.WriteLine("CollectionTreeDragDropBehavior: DragOver rejected - no source or tree.");
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                Trace.WriteLine("CollectionTreeDragDropBehavior: DragOver rejected - invalid drop target.");
                return;
            }

            if (IsAncestor(source, targetFolder))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                Trace.WriteLine("CollectionTreeDragDropBehavior: DragOver rejected - ancestor detected.");
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            Trace.WriteLine($"CollectionTreeDragDropBehavior: DragOver accepted for target folder '{targetFolder.Name}' at index {insertIndex}.");
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (!TryGetDragSource(e, out var source) || !TryGetTree(out var tree))
            {
                e.Handled = true;
                Trace.WriteLine("CollectionTreeDragDropBehavior: Drop ignored - no source or tree context.");
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Handled = true;
                Trace.WriteLine("CollectionTreeDragDropBehavior: Drop ignored - invalid drop target.");
                return;
            }

            if (IsAncestor(source, targetFolder))
            {
                e.Handled = true;
                Trace.WriteLine("CollectionTreeDragDropBehavior: Drop ignored - ancestor detected.");
                return;
            }

            if (ReferenceEquals(source.Parent ?? tree.Root, targetFolder))
            {
                var currentIndex = targetFolder.Children.IndexOf(source);
                if (currentIndex >= 0 && currentIndex < insertIndex)
                {
                    insertIndex--;
                    Trace.WriteLine($"CollectionTreeDragDropBehavior: Adjusted insert index to {insertIndex} for intra-folder move of '{source.Name}'.");
                }
            }

            var request = new CollectionDragDropRequest
            {
                Source = source,
                TargetFolder = targetFolder,
                InsertIndex = Math.Max(0, insertIndex)
            };

            if (tree.MoveFolderCommand.CanExecute(request))
            {
                Trace.TraceInformation("CollectionTreeDragDropBehavior: Executing move for '{0}' into folder '{1}' at index {2}.",
                    source.Name, targetFolder.Name, request.InsertIndex);

                await tree.MoveFolderCommand.ExecuteAsync(request);
            }
            else
            {
                Trace.TraceWarning("CollectionTreeDragDropBehavior: Move command rejected for '{0}'.", source.Name);
            }

            e.Handled = true;
        }

        private static bool TryGetDragSource(DragEventArgs e, [NotNullWhen(true)] out LibraryCollectionFolderViewModel? source)
        {
            source = e.Data.GetData(typeof(LibraryCollectionFolderViewModel)) as LibraryCollectionFolderViewModel;
            if (source is null)
            {
                Trace.WriteLine("CollectionTreeDragDropBehavior: No collection folder found in drag data.");
                return false;
            }

            Trace.WriteLine($"CollectionTreeDragDropBehavior: Drag source resolved to node '{source.Name}'.");
            return true;
        }

        private bool TryGetTree([NotNullWhen(true)] out LibraryCollectionsViewModel? tree)
        {
            tree = AssociatedObject.DataContext as LibraryCollectionsViewModel;
            return tree is not null;
        }

        private static bool TryGetDropInfo(DependencyObject? sourceElement,
                                           LibraryCollectionsViewModel tree,
                                           LibraryCollectionFolderViewModel source,
                                           out LibraryCollectionFolderViewModel targetFolder,
                                           out int insertIndex)
        {
            ArgumentNullException.ThrowIfNull(source);

            var item = FindTreeViewItem(sourceElement);
            if (item?.DataContext is LibraryCollectionFolderViewModel folder)
            {
                var position = GetDropPosition(item);
                if (position == DropPosition.Center)
                {
                    targetFolder = folder;
                    insertIndex = folder.Children.Count;
                    Trace.WriteLine($"CollectionTreeDragDropBehavior: Calculated drop INTO folder '{folder.Name}' at index {insertIndex}.");
                    return true;
                }

                targetFolder = folder.Parent ?? tree.Root;
                var siblingIndex = targetFolder.Children.IndexOf(folder);
                if (siblingIndex < 0)
                {
                    siblingIndex = targetFolder.Children.Count;
                }

                insertIndex = position == DropPosition.Before ? siblingIndex : siblingIndex + 1;
                Trace.WriteLine($"CollectionTreeDragDropBehavior: Calculated drop {(position == DropPosition.Before ? "before" : "after")} folder '{folder.Name}' at index {insertIndex} in '{targetFolder.Name}'.");
                return true;
            }

            targetFolder = tree.Root;
            insertIndex = tree.Root.Children.Count;
            return true;
        }

        private static bool IsAncestor(LibraryCollectionFolderViewModel ancestor, LibraryCollectionFolderViewModel candidate)
        {
            var current = candidate;
            while (current is not null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    Trace.WriteLine($"CollectionTreeDragDropBehavior: Preventing drop because '{ancestor.Name}' is an ancestor of '{candidate.Name}'.");
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private enum DropPosition
        {
            Before,
            Center,
            After
        }

        private static DropPosition GetDropPosition(System.Windows.Controls.TreeViewItem item)
        {
            var position = System.Windows.Input.Mouse.GetPosition(item);
            var height = item.ActualHeight;

            const double threshold = 0.25;
            if (position.Y < height * threshold)
            {
                return DropPosition.Before;
            }

            if (position.Y > height * (1 - threshold))
            {
                return DropPosition.After;
            }

            return DropPosition.Center;
        }

        private static TreeViewItem? FindTreeViewItem(DependencyObject? current)
        {
            while (current is not null && current is not TreeViewItem)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return current as TreeViewItem;
        }
    }

    public sealed class CollectionDragDropRequest
    {
        public LibraryCollectionFolderViewModel? Source { get; init; }
        public LibraryCollectionFolderViewModel? TargetFolder { get; init; }
        public int InsertIndex { get; init; }
    }
}