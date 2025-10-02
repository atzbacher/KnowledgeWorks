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
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;
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
                targetFolder = folder;
                insertIndex = folder.Children.Count;
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
                    return true;
                }

                // Navigate to parent - we'll need to add Parent property to LibraryCollectionFolderViewModel
                // For now, return false to prevent circular references
                break;
            }

            return false;
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