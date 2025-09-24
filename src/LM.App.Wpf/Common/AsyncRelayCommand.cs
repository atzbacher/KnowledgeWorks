using System;
using System.Threading.Tasks;

namespace LM.App.Wpf.Common
{
    /// <summary>
    /// Async ICommand with re-entrancy guard.
    /// Supports both parameterless (Func<Task>) and parameterized (Func<object?, Task>) delegates.
    /// </summary>
    public sealed class AsyncRelayCommand : System.Windows.Input.ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private bool _isExecuting;

        // Parameterless constructor: Func<Task> + optional Func<bool>
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            if (execute is null) throw new ArgumentNullException(nameof(execute));
            _execute = _ => execute();
            _canExecute = canExecute is null ? null : new Func<object?, bool>(_ => canExecute());
        }

        // Parameterized constructor: Func<object?, Task> + optional Func<object?, bool>
        public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            try
            {
                _isExecuting = true;
                RaiseCanExecuteChangedCore();
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChangedCore();
            }
        }

        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Manually trigger re-evaluation of CanExecute.
        /// </summary>
        public void RaiseCanExecuteChanged() => RaiseCanExecuteChangedCore();

        private void RaiseCanExecuteChangedCore()
        {
            var handlers = CanExecuteChanged;
            if (handlers is null)
                return;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => handlers(this, EventArgs.Empty));
                return;
            }

            handlers(this, EventArgs.Empty);
        }
    }
}
