#nullable enable
using System;

namespace LM.App.Wpf.Common.Dialogs
{
    internal sealed class WpfMessageBoxService : IMessageBoxService
    {
        public void Show(string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image)
        {
            System.Windows.MessageBox.Show(message, caption, buttons, image);
        }
    }
}
