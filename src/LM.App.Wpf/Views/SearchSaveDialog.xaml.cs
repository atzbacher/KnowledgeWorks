#nullable enable
using System;
using System.Globalization;
using System.Windows;
using LM.App.Wpf.Common;
using LM.Core.Models;

namespace LM.App.Wpf.Views
{
    internal partial class SearchSaveDialog : Window
    {
        public string ResultName { get; private set; } = string.Empty;
        public string ResultNotes { get; private set; } = string.Empty;
        public string ResultTagsRaw { get; private set; } = string.Empty;

        public SearchSaveDialog(SearchSavePromptContext context)
        {
            InitializeComponent();

            QueryText.Text = context.Query;
            DatabaseText.Text = context.Database == SearchDatabase.PubMed ? "PubMed" : "ClinicalTrials.gov";
            RangeText.Text = FormatRange(context.From, context.To);
            Loaded += (_, _) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private static string FormatRange(DateTime? from, DateTime? to)
        {
            if (!from.HasValue && !to.HasValue)
                return "–";

            string Format(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "–";
            return $"{Format(from)} → {Format(to)}";
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                System.Windows.MessageBox.Show(this, "Please enter a name for the search.", "Save search", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }

            ResultName = NameBox.Text.Trim();
            ResultNotes = NotesBox.Text.Trim();
            ResultTagsRaw = TagsBox.Text?.Trim() ?? string.Empty;
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
