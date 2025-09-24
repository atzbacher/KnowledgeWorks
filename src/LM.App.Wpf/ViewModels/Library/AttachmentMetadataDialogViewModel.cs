#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Library;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels.Library
{
    internal sealed partial class AttachmentMetadataDialogViewModel : DialogViewModelBase
    {
        private AttachmentMetadataPromptResult? _result;

        public AttachmentMetadataDialogViewModel()
        {
            Items = new ObservableCollection<AttachmentMetadataItemViewModel>();
            Title = "Add attachments";
            KindOptions = Enum.GetValues(typeof(AttachmentKind));
        }

        public ObservableCollection<AttachmentMetadataItemViewModel> Items { get; }

        public string Title { get; private set; }

        public string EntryTitle { get; private set; } = string.Empty;

        public Array KindOptions { get; }

        public void Initialize(AttachmentMetadataPromptContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            Items.Clear();
            EntryTitle = context.EntryTitle ?? string.Empty;
            _result = null;

            foreach (var path in context.FilePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var displayName = Path.GetFileName(path) ?? path;
                var defaultTitle = Path.GetFileNameWithoutExtension(path);

                Items.Add(new AttachmentMetadataItemViewModel(path, displayName)
                {
                    Title = string.IsNullOrWhiteSpace(defaultTitle) ? displayName : defaultTitle!,
                    Kind = AttachmentKind.Supplement,
                    Tags = string.Empty
                });
            }
        }

        public AttachmentMetadataPromptResult? BuildResult() => _result;

        [RelayCommand]
        private void Cancel()
        {
            _result = null;
            RequestClose(false);
        }

        [RelayCommand]
        private void Save()
        {
            var selections = new List<AttachmentMetadataSelection>();

            foreach (var item in Items)
            {
                if (item is null)
                    continue;

                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    System.Windows.MessageBox.Show(
                        "Each attachment must have a title.",
                        "Add Attachments",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var tags = ParseTags(item.Tags);
                selections.Add(new AttachmentMetadataSelection(item.SourcePath, item.Title.Trim(), item.Kind, tags));
            }

            _result = new AttachmentMetadataPromptResult(selections);
            RequestClose(true);
        }

        private static IReadOnlyList<string> ParseTags(string? tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
                return Array.Empty<string>();

            var split = tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length == 0)
                return Array.Empty<string>();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            foreach (var tag in split)
            {
                if (set.Add(tag))
                    list.Add(tag);
            }

            return list;
        }
    }

    internal sealed partial class AttachmentMetadataItemViewModel : ObservableObject
    {
        public AttachmentMetadataItemViewModel(string sourcePath, string displayName)
        {
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            DisplayName = displayName ?? string.Empty;
        }

        public string SourcePath { get; }

        public string DisplayName { get; }

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private AttachmentKind kind = AttachmentKind.Supplement;

        [ObservableProperty]
        private string tags = string.Empty;
    }
}
