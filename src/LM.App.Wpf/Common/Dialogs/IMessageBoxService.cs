#nullable enable
using System;

namespace LM.App.Wpf.Common.Dialogs
{
    internal interface IMessageBoxService
    {
        void Show(string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image);
    }
}
