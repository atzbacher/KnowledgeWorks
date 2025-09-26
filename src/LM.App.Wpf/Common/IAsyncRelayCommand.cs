using System;

namespace LM.App.Wpf.Common
{
    /// <summary>
    /// Contract for asynchronous commands that expose requery notifications.
    /// </summary>
    public interface IAsyncRelayCommand : System.Windows.Input.ICommand
    {
        /// <summary>
        /// Requests that <see cref="CanExecute(object?)"/> be re-evaluated.
        /// </summary>
        void RaiseCanExecuteChanged();
    }
}
