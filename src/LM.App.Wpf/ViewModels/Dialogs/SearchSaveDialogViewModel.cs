using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed partial class SearchSaveDialogViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string query = string.Empty;

        [ObservableProperty]
        private string database = string.Empty;

        [ObservableProperty]
        private string range = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string tags = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        public string Title => "Save search";

        public string ResultName { get; private set; } = string.Empty;
        public string ResultNotes { get; private set; } = string.Empty;
        public string ResultTags { get; private set; } = string.Empty;

        public void Initialize(SearchSavePromptContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            Query = context.Query;
            Database = context.Database == SearchDatabase.PubMed ? "PubMed" : "ClinicalTrials.gov";
            Range = FormatRange(context.From, context.To);
            Name = context.DefaultName ?? string.Empty;
            Notes = context.DefaultNotes ?? string.Empty;
            Tags = context.DefaultTags is null || context.DefaultTags.Count == 0
                ? string.Empty
                : string.Join(", ", context.DefaultTags);
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                System.Windows.MessageBox.Show("Please enter a name for the search.",
                                               "Save search",
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Warning);
                return;
            }

            ResultName = Name.Trim();
            ResultNotes = Notes.Trim();
            ResultTags = Tags?.Trim() ?? string.Empty;
            RequestClose(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose(false);
        }

        private static string FormatRange(DateTime? from, DateTime? to)
        {
            if (!from.HasValue && !to.HasValue)
                return "–";

            string Format(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "–";
            return $"{Format(from)} → {Format(to)}";
        }
    }
}
