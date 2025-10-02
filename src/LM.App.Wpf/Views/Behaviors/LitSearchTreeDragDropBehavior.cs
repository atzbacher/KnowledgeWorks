using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using LM.App.Wpf.ViewModels.Library.LitSearch;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    public sealed class LitSearchTreeDragDropBehavior : Behavior<System.Windows.Controls.TreeView>
    {
        private System.Windows.Point? _dragStart;
        private LitSearchNodeViewModel? _dragSource;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = FindTreeViewItem(e.OriginalSource as System.Windows.DependencyObject);
            if (item?.DataContext is LitSearchNodeViewModel node && node.IsDraggable)
            {
                _dragStart = e.GetPosition(AssociatedObject);
                _dragSource = node;
                Trace.WriteLine($"[LitSearchTreeDragDropBehavior] Drag start captured for '{node.Id}'.");
            }
            else
            {
                _dragStart = null;
                _dragSource = null;
            }
        }

        private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStart is null || _dragSource is null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                return;
            }

            var current = e.GetPosition(AssociatedObject);
            if (Math.Abs(current.X - _dragStart.Value.X) < System.Windows.SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStart.Value.Y) < System.Windows.SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var data = new System.Windows.DataObject(typeof(LitSearchNodeViewModel), _dragSource);
            Trace.WriteLine($"[LitSearchTreeDragDropBehavior] Initiating drag for '{_dragSource.Id}'.");
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
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as System.Windows.DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (source is LitSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }

        private async void OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (!TryGetDragSource(e, out var source) || !TryGetTree(out var tree))
            {
                e.Handled = true;
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as System.Windows.DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Handled = true;
                return;
            }

            if (source is LitSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
            {
                e.Handled = true;
                return;
            }

            if (source is LitSearchNodeViewModel && source == targetFolder)
            {
                e.Handled = true;
                return;
            }

            if (source is LitSearchEntryViewModel entry && ReferenceEquals(entry.Parent, targetFolder))
            {
                var currentIndex = targetFolder.Children.IndexOf(entry);
                if (currentIndex < insertIndex)
                {
                    insertIndex--;
                }
            }
            else if (source is LitSearchFolderViewModel movingFolder && ReferenceEquals(movingFolder.Parent, targetFolder))
            {
                var currentIndex = targetFolder.Children.IndexOf(movingFolder);
                if (currentIndex < insertIndex)
                {
                    insertIndex--;
                }
            }

            var request = new LitSearchDragDropRequest
            {
                Source = source,
                TargetFolder = targetFolder,
                InsertIndex = Math.Max(0, insertIndex)
            };

            if (tree.MoveCommand.CanExecute(request))
            {
                Trace.WriteLine($"[LitSearchTreeDragDropBehavior] Executing move for '{source.Id}' into '{targetFolder.Id}' at {request.InsertIndex}.");
                await tree.MoveCommand.ExecuteAsync(request);
            }

            e.Handled = true;
        }

        private static bool TryGetDragSource(System.Windows.DragEventArgs e, [NotNullWhen(true)] out LitSearchNodeViewModel? source)
        {
            source = e.Data.GetData(typeof(LitSearchNodeViewModel)) as LitSearchNodeViewModel;
            return source is not null;
        }

        private bool TryGetTree([NotNullWhen(true)] out LitSearchTreeViewModel? tree)
        {
            tree = AssociatedObject.DataContext as LitSearchTreeViewModel;
            return tree is not null;
        }

        private static bool TryGetDropInfo(System.Windows.DependencyObject? element,
                                           LitSearchTreeViewModel tree,
                                           LitSearchNodeViewModel source,
                                           out LitSearchFolderViewModel targetFolder,
                                           out int insertIndex)
        {
            var item = FindTreeViewItem(element);
            switch (item?.DataContext)
            {
                case LitSearchFolderViewModel folder:
                    targetFolder = folder;
                    insertIndex = folder.Children.Count;
                    return true;
                case LitSearchEntryViewModel entry:
                    targetFolder = entry.Parent ?? tree.Root;
                    insertIndex = targetFolder.Children.IndexOf(entry);
                    return true;
                case LitSearchRunViewModel run:
                    targetFolder = run.Parent.Parent ?? tree.Root;
                    insertIndex = targetFolder.Children.IndexOf(run.Parent);
                    return true;
                default:
                    targetFolder = source switch
                    {
                        LitSearchFolderViewModel folderSource => folderSource.Parent ?? tree.Root,
                        LitSearchEntryViewModel entrySource => entrySource.Parent ?? tree.Root,
                        _ => tree.Root
                    };
                    insertIndex = targetFolder.Children.Count;
                    return true;
            }
        }

        private static bool IsAncestor(LitSearchFolderViewModel ancestor, LitSearchFolderViewModel candidate)
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
