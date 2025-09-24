#nullable enable
using LM.App.Wpf.Common;

namespace LM.App.Wpf.ViewModels
{
    public sealed class SearchItemViewModel : ViewModelBase
    {
        public string Header { get; }
        public LibraryViewModel Vm { get; }

        public SearchItemViewModel(string header, LibraryViewModel vm)
        {
            Header = header;
            Vm = vm;
        }
    }
}
