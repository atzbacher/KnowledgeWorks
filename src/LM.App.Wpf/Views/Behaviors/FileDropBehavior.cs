using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    internal sealed class FileDropBehavior : Behavior<System.Windows.FrameworkElement>
    {
        public static readonly System.Windows.DependencyProperty PreviewDragOverCommandProperty =
            System.Windows.DependencyProperty.Register(
                nameof(PreviewDragOverCommand),
                typeof(System.Windows.Input.ICommand),
                typeof(FileDropBehavior));

        public static readonly System.Windows.DependencyProperty DropCommandProperty =
            System.Windows.DependencyProperty.Register(
                nameof(DropCommand),
                typeof(System.Windows.Input.ICommand),
                typeof(FileDropBehavior));

        public System.Windows.Input.ICommand? PreviewDragOverCommand
        {
            get => (System.Windows.Input.ICommand?)GetValue(PreviewDragOverCommandProperty);
            set => SetValue(PreviewDragOverCommandProperty, value);
        }

        public System.Windows.Input.ICommand? DropCommand
        {
            get => (System.Windows.Input.ICommand?)GetValue(DropCommandProperty);
            set => SetValue(DropCommandProperty, value);
        }

        /// <summary>
        /// When <c>true</c>, attempts to resolve the drop target by walking the visual tree for a <see cref="System.Windows.Controls.DataGridRow"/>.
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

        private void OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
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
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void OnDrop(object sender, System.Windows.DragEventArgs e)
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

        private FileDropRequest CreateRequest(System.Windows.DragEventArgs e)
        {
            var paths = ExtractPaths(e);
            var target = UseDataGridRowContext
                ? ResolveRowContext(e.OriginalSource as System.Windows.DependencyObject)
                : null;
            return new FileDropRequest(paths, target, e);
        }

        private static IReadOnlyList<string> ExtractPaths(System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return Array.Empty<string>();

            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] raw || raw.Length == 0)
                return Array.Empty<string>();

            return raw
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => path.Trim())
                .ToArray();
        }

        private object? ResolveRowContext(System.Windows.DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is System.Windows.Controls.DataGridRow row)
                    return row.DataContext;

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }

    internal sealed record FileDropRequest(
        IReadOnlyList<string> Paths,
        object? DropTarget,
        System.Windows.DragEventArgs Args);
}
