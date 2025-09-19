#nullable enable
using System.Windows;
using LM.App.Wpf.Common;

namespace LM.App.Wpf.Views
{
    internal partial class LibraryPresetSaveDialog : Window
    {
        public string ResultName { get; private set; } = string.Empty;

        public LibraryPresetSaveDialog(LibraryPresetSaveContext context)
        {
            InitializeComponent();

            NameBox.Text = context.DefaultName;
            Loaded += (_, _) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Please provide a name for the preset.", "Save Library Preset", MessageBoxButton.OK, MessageBoxImage.Information);
                NameBox.Focus();
                return;
            }

            ResultName = name;
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
