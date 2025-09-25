using System;
using System.Collections.Specialized;
using System.Text;
using LM.App.Wpf.ViewModels.Library;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    /// <summary>
    /// Binds data grid column visibility to <see cref="LibraryColumnVisibility" />.
    /// </summary>
    public sealed class DataGridColumnVisibilityBehavior : Behavior<System.Windows.Controls.DataGrid>
    {
        private static readonly System.Windows.DependencyProperty VisibilityMapProperty = System.Windows.DependencyProperty.Register(
            nameof(VisibilityMap),
            typeof(LibraryColumnVisibility),
            typeof(DataGridColumnVisibilityBehavior),
            new System.Windows.PropertyMetadata(null, OnVisibilityMapChanged));

        public static readonly System.Windows.DependencyProperty ColumnKeyProperty = System.Windows.DependencyProperty.RegisterAttached(
            "ColumnKey",
            typeof(string),
            typeof(DataGridColumnVisibilityBehavior),
            new System.Windows.PropertyMetadata(null));

        public LibraryColumnVisibility? VisibilityMap
        {
            get => (LibraryColumnVisibility?)GetValue(VisibilityMapProperty);
            set => SetValue(VisibilityMapProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject is null)
            {
                return;
            }

            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Columns.CollectionChanged += OnColumnsChanged;
            ApplyBindings();
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.Loaded -= OnLoaded;
                AssociatedObject.Columns.CollectionChanged -= OnColumnsChanged;
            }

            base.OnDetaching();
        }

        private static void OnVisibilityMapChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGridColumnVisibilityBehavior behavior)
            {
                behavior.ApplyBindings();
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyBindings();
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ApplyBindings();
        }

        private void ApplyBindings()
        {
            if (AssociatedObject is null || VisibilityMap is null)
            {
                return;
            }

            foreach (var column in AssociatedObject.Columns)
            {
                if (column is null)
                {
                    continue;
                }

                var key = ResolveColumnKey(column);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var binding = new System.Windows.Data.Binding($"[{key}]")
                {
                    Source = VisibilityMap,
                    Converter = new System.Windows.Controls.BooleanToVisibilityConverter(),
                    Mode = System.Windows.Data.BindingMode.OneWay
                };

                System.Windows.Data.BindingOperations.SetBinding(
                    column,
                    System.Windows.Controls.DataGridColumn.VisibilityProperty,
                    binding);
            }
        }

        public static void SetColumnKey(System.Windows.DependencyObject element, string? value)
        {
            element?.SetValue(ColumnKeyProperty, value);
        }

        public static string? GetColumnKey(System.Windows.DependencyObject element)
        {
            return element is null ? null : (string?)element.GetValue(ColumnKeyProperty);
        }

        private static string? ResolveColumnKey(System.Windows.Controls.DataGridColumn column)
        {
            var explicitKey = GetColumnKey(column);
            if (!string.IsNullOrWhiteSpace(explicitKey))
            {
                return explicitKey;
            }

            if (column.Header is null)
            {
                return null;
            }

            var headerText = column.Header switch
            {
                string text => text,
                System.Windows.Controls.TextBlock textBlock => textBlock.Text,
                _ => column.Header.ToString()
            };

            if (string.IsNullOrWhiteSpace(headerText))
            {
                return null;
            }

            var segments = headerText
                .Split(new[] { ' ', '\t', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            var builder = new System.Text.StringBuilder();
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(segment[0]));
                if (segment.Length > 1)
                {
                    builder.Append(segment.Substring(1).ToLowerInvariant());
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
    }
}
