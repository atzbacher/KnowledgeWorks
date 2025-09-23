#nullable enable
using System;
using System.Linq;
using System.Windows;
using LM.App.Wpf.ViewModels.Add;
using LM.Infrastructure.Utils;

namespace LM.App.Wpf.Views
{
    public partial class StagingEditorWindow : Window
    {
        private StagingListViewModel VM => (StagingListViewModel)DataContext;

        public StagingEditorWindow(StagingListViewModel vm)
        {
            InitializeComponent();
            DataContext = vm ?? throw new ArgumentNullException(nameof(vm));
            // VM already exposes: Current, SelectedType, EntryTypes, IndexLabel, SelectByOffset(...)
        }

        private void OnPrev(object sender, RoutedEventArgs e) => VM.SelectByOffset(-1);
        private void OnNext(object sender, RoutedEventArgs e) => VM.SelectByOffset(+1);
        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnGenerateShortTitle(object sender, RoutedEventArgs e)
        {
            var cur = VM.Current;
            if (cur is null) return;

            var authors = (cur.AuthorsCsv ?? "")
                          .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(a => a.Trim())
                          .ToList();

            cur.DisplayName = BibliographyHelper.GenerateShortTitle(
                cur.Title, authors, cur.Source, cur.Year);
        }
    }
}
