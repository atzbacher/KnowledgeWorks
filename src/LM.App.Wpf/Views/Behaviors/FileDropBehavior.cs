using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    internal sealed class FileDropBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty PreviewDragOverCommandProperty =
            DependencyProperty.Register(nameof(PreviewDragOverCommand), typeof(ICommand), typeof(FileDropBehavior));

        public static readonly DependencyProperty DropCommandProperty =
            DependencyProperty.Register(nameof(DropCommand), typeof(ICommand), typeof(FileDropBehavior));

        public ICommand? PreviewDragOverCommand
        {
            get => (ICommand?)GetValue(PreviewDragOverCommandProperty);
            set => SetValue(PreviewDragOverCommandProperty, value);
        }

        public ICommand? DropCommand
        {
            get => (ICommand?)GetValue(DropCommandProperty);
            set => SetValue(DropCommandProperty, value);
        }

        /// <summary>
        /// When <c>true</c>, attempts to resolve the drop target by walking the visual tree for a <see cref="DataGridRow"/>.
        /// </summary>
        public bool UseDataGridRowContext { get; set; }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.PreviewDragOver += OnPreviewDragOver;
            AssociatedObject.Drop += OnDrop;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewDragOver -= OnPreviewDragOver;
            AssociatedObject.Drop -= OnDrop;

            base.OnDetaching();
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            var command = PreviewDragOverCommand;
            if (command is null)
                return;

            var request = CreateRequest(e);
            if (command.CanExecute(request))
            {
                command.Execute(request);
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var command = DropCommand;
            if (command is null)
                return;

            var request = CreateRequest(e);
            if (command.CanExecute(request))
            {
                command.Execute(request);
            }

            e.Handled = true;
        }

        private FileDropRequest CreateRequest(DragEventArgs e)
        {
            var paths = ExtractPaths(e);
            var target = UseDataGridRowContext ? ResolveRowContext(e.OriginalSource as DependencyObject) : null;
            return new FileDropRequest(paths, target, e);
        }

        private static IReadOnlyList<string> ExtractPaths(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return Array.Empty<string>();

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] raw || raw.Length == 0)
                return Array.Empty<string>();

            return raw
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => path.Trim())
                .ToArray();
        }

        private object? ResolveRowContext(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is DataGridRow row)
                    return row.DataContext;

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }

    internal sealed record FileDropRequest(IReadOnlyList<string> Paths, object? DropTarget, DragEventArgs Args);
}
