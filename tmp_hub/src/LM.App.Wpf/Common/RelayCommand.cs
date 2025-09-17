using System;
using System.Windows.Input;

namespace LM.App.Wpf.Common
{
    public sealed class RelayCommand : ICommand
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
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
