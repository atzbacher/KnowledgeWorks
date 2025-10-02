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
            if (item?.DataContext is SavedSearchNodeViewModel node)
            {
                _dragStart = e.GetPosition(AssociatedObject);
                _dragSource = node;
                Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Drag start captured for node '{node.Id}' at {_dragStart.Value}.");
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

            var currentPosition = e.GetPosition(AssociatedObject);
            if (Math.Abs(currentPosition.X - _dragStart.Value.X) < System.Windows.SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _dragStart.Value.Y) < System.Windows.SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

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
                return;
            }

            if (!TryGetDropInfo(e.OriginalSource as System.Windows.DependencyObject, tree, source, out var targetFolder, out var insertIndex))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (source is SavedSearchFolderViewModel folder && IsAncestor(folder, targetFolder))
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
                Trace.WriteLine($"[SavedSearchTreeDragDropBehavior] Executing move for '{source.Id}' into '{targetFolder.Id}' at index {request.InsertIndex}.");
                await tree.MoveCommand.ExecuteAsync(request);
            }

            e.Handled = true;
        }

        private static bool TryGetDragSource(System.Windows.DragEventArgs e, [NotNullWhen(true)] out SavedSearchNodeViewModel? source)
        {
            source = e.Data.GetData(typeof(SavedSearchNodeViewModel)) as SavedSearchNodeViewModel;
            return source is not null;
        }

        private bool TryGetTree([NotNullWhen(true)] out SavedSearchTreeViewModel? tree)
        {
            tree = AssociatedObject.DataContext as SavedSearchTreeViewModel;
            return tree is not null;
        }

        private static bool TryGetDropInfo(System.Windows.DependencyObject? sourceElement,
                                           SavedSearchTreeViewModel tree,
                                           SavedSearchNodeViewModel source,
                                           out SavedSearchFolderViewModel targetFolder,
                                           out int insertIndex)
        {
            ArgumentNullException.ThrowIfNull(source);

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
                    targetFolder = source.Parent ?? tree.Root;
                    insertIndex = targetFolder.Children.Count;
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
