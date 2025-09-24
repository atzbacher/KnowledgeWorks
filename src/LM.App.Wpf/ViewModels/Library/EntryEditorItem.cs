#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels.Library
{
    internal sealed partial class EntryEditorItem : ObservableObject
    {
        [ObservableProperty]
        private EntryType type;

        [ObservableProperty]
        private string? title;

        [ObservableProperty]
        private string? displayName;

        [ObservableProperty]
        private string? authorsCsv;

        [ObservableProperty]
        private int? year;

        [ObservableProperty]
        private string? source;

        [ObservableProperty]
        private string? tagsCsv;

        [ObservableProperty]
        private bool isInternal;

        [ObservableProperty]
        private string? doi;

        [ObservableProperty]
        private string? pmid;

        [ObservableProperty]
        private string? internalId;

        [ObservableProperty]
        private string? notes;

        [ObservableProperty]
        private string? originalFileName;
    }
}
