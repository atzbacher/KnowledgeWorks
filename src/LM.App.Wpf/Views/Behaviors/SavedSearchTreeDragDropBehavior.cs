using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LM.App.Wpf.ViewModels.Library.SavedSearches;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    public sealed class SavedSearchTreeDragDropBehavior : Behavior<TreeView>
    {
        private Point? _dragStart;
        private SavedSearchNodeViewModel? _dragSource;

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

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (item?.DataContext is SavedSearchNodeViewModel node)
            {
                _dragStart = e.GetPosition(AssociatedObject);
                _dragSource = node;
            }
            else
            {
                _dragStart = null;
                _dragSource = null;
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
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

            var data = new DataObject(typeof(SavedSearchNodeViewModel), _dragSource);
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
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (source is SavedSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (!TryGetDragSource(e, out var source) || !TryGetTree(out var tree))
            {
                e.Handled = true;
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Handled = true;
                return;
            }

            if (source is SavedSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
            {
                e.Handled = true;
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
                await tree.MoveCommand.ExecuteAsync(request);
            }

            e.Handled = true;
        }

        private static bool TryGetDragSource(DragEventArgs e, out SavedSearchNodeViewModel? source)
        {
            source = e.Data.GetData(typeof(SavedSearchNodeViewModel)) as SavedSearchNodeViewModel;
            return source is not null;
        }

        private bool TryGetTree(out SavedSearchTreeViewModel tree)
        {
            tree = AssociatedObject.DataContext as SavedSearchTreeViewModel;
            return tree is not null;
        }

        private static bool TryGetDropInfo(DependencyObject? sourceElement,
                                           SavedSearchTreeViewModel tree,
                                           out SavedSearchFolderViewModel targetFolder,
                                           out int insertIndex)
        {
            var item = FindTreeViewItem(sourceElement);
            switch (item?.DataContext)
            {
                case SavedSearchFolderViewModel folder:
                    targetFolder = folder;
                    insertIndex = folder.Children.Count;
                    return true;
                case SavedSearchPresetViewModel preset:
                    targetFolder = preset.Parent ?? tree.Root;
                    insertIndex = targetFolder.Children.IndexOf(preset);
                    return true;
                default:
                    targetFolder = tree.Root;
                    insertIndex = tree.Root.Children.Count;
                    return true;
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

        private static TreeViewItem? FindTreeViewItem(DependencyObject? current)
        {
            while (current is not null && current is not TreeViewItem)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            return current as TreeViewItem;
        }
    }
}
