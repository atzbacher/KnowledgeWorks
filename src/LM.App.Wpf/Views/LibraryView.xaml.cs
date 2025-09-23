using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LM.App.Wpf.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;


namespace LM.App.Wpf.Views
{
    public partial class LibraryView : System.Windows.Controls.UserControl
    {
        public LibraryView() { InitializeComponent(); }

        private void LibraryDetailDragOver(object sender, DragEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm || !TryGetFilePaths(e, out var paths) || !vm.Results.CanAcceptFileDrop(paths))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Copy;
            }

            e.Handled = true;
        }

        private void LibraryResultsDragOver(object sender, DragEventArgs e)
        {
            var dropTarget = ResolveDropTarget(e.OriginalSource);
            if (DataContext is not LibraryViewModel vm || !TryGetFilePaths(e, out var paths) || !vm.Results.CanAcceptFileDrop(paths, dropTarget))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Copy;
            }

            e.Handled = true;
        }

        private async void LibraryDetailDrop(object sender, DragEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm || !TryGetFilePaths(e, out var paths))
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;

            try
            {
                await vm.Results.HandleFileDropAsync(paths);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LibraryView] Drop failed: {ex}");
            }
        }

        private async void LibraryResultsDrop(object sender, DragEventArgs e)
        {
            var dropTarget = ResolveDropTarget(e.OriginalSource);
            if (DataContext is not LibraryViewModel vm || !TryGetFilePaths(e, out var paths))
            {
                e.Handled = true;
                return;
            }

            if (dropTarget is not null && !ReferenceEquals(vm.Results.Selected, dropTarget))
                vm.Results.Selected = dropTarget;

            e.Handled = true;

            try
            {
                await vm.Results.HandleFileDropAsync(paths, dropTarget);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LibraryView] Drop failed: {ex}");
            }
        }

        private static bool TryGetFilePaths(DragEventArgs e, out string[] paths)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    paths = files;
                    return true;
                }
            }

            paths = Array.Empty<string>();
            return false;
        }

        private static LibrarySearchResult? ResolveDropTarget(object? originalSource)
        {
            if (originalSource is not DependencyObject element)
                return null;

            var row = FindAncestor<DataGridRow>(element);
            return row?.DataContext as LibrarySearchResult;
        }

        private static T? FindAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
