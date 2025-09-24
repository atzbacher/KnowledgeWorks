using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.Common.Dialogs
{
    /// <summary>
    /// Base class for view models that back modal dialogs.
    /// </summary>
    public abstract class DialogViewModelBase : ObservableObject
    {
        public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

        /// <summary>
        /// Requests that the owning dialog close with the supplied result.
        /// </summary>
        /// <param name="dialogResult">The result that should be applied to the dialog window.</param>
        protected void RequestClose(bool? dialogResult)
        {
            CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult));
        }
    }

    /// <summary>
    /// Event arguments used when a dialog requests to close.
    /// </summary>
    public sealed class DialogCloseRequestedEventArgs : EventArgs
    {
        public DialogCloseRequestedEventArgs(bool? dialogResult)
        {
            DialogResult = dialogResult;
        }

        public bool? DialogResult { get; }
    }
}
