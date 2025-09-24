using System;

namespace LM.App.Wpf.Common
{
    public sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _exec;
        private readonly Func<object?, bool>? _can;

        public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
        {
            _exec = exec ?? throw new ArgumentNullException(nameof(exec));
            _can = can;
        }

        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _exec(parameter);
        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
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
