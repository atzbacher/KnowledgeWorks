using System;
using System.Diagnostics;
using System.Windows.Controls;
using LM.App.Wpf.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace LM.App.Wpf.Views
{
    public partial class LibraryView : UserControl
    {
        public LibraryView() { InitializeComponent(); }

        private void LibraryDetailDragOver(object sender, DragEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm || !TryGetFilePaths(e, out var paths) || !vm.CanAcceptFileDrop(paths))
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
                await vm.HandleFileDropAsync(paths);
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
    }
}
