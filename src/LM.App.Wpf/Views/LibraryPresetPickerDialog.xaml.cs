#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using LM.App.Wpf.Common;

namespace LM.App.Wpf.Views
{
    internal partial class LibraryPresetPickerDialog : Window
    {
        private readonly ObservableCollection<LibraryPresetSummary> _presets;
        private readonly bool _allowLoad;
        private readonly List<string> _deleted = new();

        public string? SelectedPresetName { get; private set; }
        public IReadOnlyList<string> DeletedPresetNames => _deleted;

        public LibraryPresetPickerDialog(LibraryPresetSelectionContext context)
        {
            InitializeComponent();

            Title = context.Title;
            HeaderText.Text = context.Title;
            _allowLoad = context.AllowLoad;
            LoadButton.Visibility = _allowLoad ? Visibility.Visible : Visibility.Collapsed;
            LoadButton.IsEnabled = _allowLoad;

            _presets = new ObservableCollection<LibraryPresetSummary>(context.Presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase));
            PresetList.ItemsSource = _presets;
            DeleteButton.IsEnabled = _presets.Count > 0;
            if (!_allowLoad)
            {
                LoadButton.IsEnabled = false;
            }
            else
            {
                LoadButton.IsEnabled = _presets.Count > 0;
            }

            Loaded += (_, _) =>
            {
                if (_presets.Count > 0)
                {
                    PresetList.SelectedIndex = 0;
                    PresetList.Focus();
                }
            };
        }

        private void OnLoad(object? sender, RoutedEventArgs e)
        {
            if (!_allowLoad)
                return;

            if (PresetList.SelectedItem is not LibraryPresetSummary summary)
            {
                MessageBox.Show(this, "Select a preset to load.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPresetName = summary.Name;
            DialogResult = true;
        }

        private void OnDelete(object? sender, RoutedEventArgs e)
        {
            if (PresetList.SelectedItem is not LibraryPresetSummary summary)
            {
                MessageBox.Show(this, "Select a preset to delete.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(this, $"Delete preset \"{summary.Name}\"?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            _deleted.Add(summary.Name);
            _presets.Remove(summary);

            if (_presets.Count == 0)
            {
                DeleteButton.IsEnabled = false;
                if (_allowLoad)
                    LoadButton.IsEnabled = false;
            }
        }

        private void OnClose(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
