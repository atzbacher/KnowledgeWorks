using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.Common.Dialogs
{
    internal abstract partial class DialogViewModelBase : ObservableObject
    {
        public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

        protected void RequestClose(bool? dialogResult)
        {
            CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult));
        }
    }

    internal sealed class DialogCloseRequestedEventArgs : EventArgs
    {
        public DialogCloseRequestedEventArgs(bool? dialogResult)
        {
            DialogResult = dialogResult;
        }

        public bool? DialogResult { get; }
    }
}
