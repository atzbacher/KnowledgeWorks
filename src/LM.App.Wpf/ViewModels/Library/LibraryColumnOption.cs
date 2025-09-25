using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library
{
    /// <summary>Represents a togglable column in the Library results grid.</summary>
    public sealed partial class LibraryColumnOption : ObservableObject
    {
        public LibraryColumnOption(string key, string displayName, bool isVisible)
        {
            Key = key;
            DisplayName = displayName;
            this.isVisible = isVisible;
        }

        public string Key { get; }

        public string DisplayName { get; }

        [ObservableProperty]
        private bool isVisible;
    }
}

