using Microsoft.Xaml.Behaviors;

namespace LM.App.Wpf.Views.Behaviors
{
    internal sealed class SelectAllTextOnFocusBehavior : Behavior<System.Windows.Controls.TextBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.GotKeyboardFocus += OnGotKeyboardFocus;
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.GotKeyboardFocus -= OnGotKeyboardFocus;
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            base.OnDetaching();
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AssociatedObject.SelectAll();
        }

        private void OnGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            AssociatedObject.SelectAll();
        }

        private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (AssociatedObject.IsKeyboardFocusWithin)
                return;

            e.Handled = true;
            AssociatedObject.Focus();
        }
    }
}
