using LM.Review.Core.Models;

namespace LM.App.Wpf.Views.Review.Controls
{
    public partial class DecisionBadge : System.Windows.Controls.UserControl
    {
        private static readonly System.Windows.Media.Brush DefaultForeground = System.Windows.Media.Brushes.White;

        public DecisionBadge()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private static readonly System.Windows.DependencyProperty StatusProperty = System.Windows.DependencyProperty.Register(
            nameof(Status),
            typeof(ScreeningStatus),
            typeof(DecisionBadge),
            new System.Windows.PropertyMetadata(ScreeningStatus.Pending, OnStatusChanged));

        public ScreeningStatus Status
        {
            get => (ScreeningStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        private void OnLoaded(object? sender, System.Windows.RoutedEventArgs e)
        {
            UpdateVisualState();
        }

        private static void OnStatusChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is DecisionBadge badge)
            {
                badge.UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            var (label, backgroundKey) = Status switch
            {
                ScreeningStatus.Included => ("Included", "ScreeningStatusIncludedBrush"),
                ScreeningStatus.Excluded => ("Excluded", "ScreeningStatusExcludedBrush"),
                ScreeningStatus.Escalated => ("Escalated", "ScreeningStatusEscalatedBrush"),
                ScreeningStatus.Pending => ("Pending", "ScreeningStatusPendingBrush"),
                _ => (Status.ToString(), "ScreeningStatusDefaultBrush")
            };

            BadgeText.Text = label;
            BadgeText.Foreground = DefaultForeground;

            if (TryFindResource(backgroundKey) is System.Windows.Media.Brush brush)
            {
                BadgeBorder.Background = brush;
            }
            else
            {
                BadgeBorder.Background = System.Windows.Media.Brushes.Gray;
            }
        }
    }
}
