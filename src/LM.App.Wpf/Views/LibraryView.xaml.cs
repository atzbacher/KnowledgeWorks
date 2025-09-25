using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Library;

namespace LM.App.Wpf.Views
{
    public partial class LibraryView : System.Windows.Controls.UserControl
    {
        public LibraryView() => InitializeComponent();

        private async void OnNavigationSelected(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not LibraryViewModel vm)
            {
                return;
            }

            if (e.NewValue is LibraryNavigationNodeViewModel node)
            {
                await vm.HandleNavigationSelectionAsync(node).ConfigureAwait(false);
            }
        }
    }
}
