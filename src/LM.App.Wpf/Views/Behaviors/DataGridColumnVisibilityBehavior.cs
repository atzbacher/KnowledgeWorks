using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LM.App.Wpf.ViewModels.Library;
using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    /// <summary>
    /// Binds data grid column visibility to <see cref="LibraryColumnVisibility" />.
    /// </summary>
    public sealed class DataGridColumnVisibilityBehavior : Behavior<DataGrid>
    {
        public static readonly DependencyProperty VisibilityMapProperty = DependencyProperty.Register(
            nameof(VisibilityMap),
            typeof(LibraryColumnVisibility),
            typeof(DataGridColumnVisibilityBehavior),
            new PropertyMetadata(null, OnVisibilityMapChanged));

        public LibraryColumnVisibility? VisibilityMap
        {
            get => (LibraryColumnVisibility?)GetValue(VisibilityMapProperty);
            set => SetValue(VisibilityMapProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject is null)
                return;

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

        private static void OnVisibilityMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGridColumnVisibilityBehavior behavior)
            {
                behavior.ApplyBindings();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => ApplyBindings();

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyBindings();

        private void ApplyBindings()
        {
            if (AssociatedObject is null || VisibilityMap is null)
                return;

            foreach (var column in AssociatedObject.Columns)
            {
                if (column is null)
                    continue;

                if (column.Tag is not string key)
                    continue;

                var binding = new Binding($"[{key}]")
                {
                    Source = VisibilityMap,
                    Converter = new BooleanToVisibilityConverter(),
                    Mode = BindingMode.OneWay
                };

                BindingOperations.SetBinding(column, DataGridColumn.VisibilityProperty, binding);
            }
        }
    }
}

