namespace LM.App.Wpf.ViewModels.Review;

using System.ComponentModel;
using System.Runtime.CompilerServices;

internal sealed class ReviewStepViewModel : INotifyPropertyChanged
{
    private bool _isActive;

    public ReviewStepViewModel(string title, ReviewWorkflowStep step)
    {
        Title = title;
        Step = step;
    }

    public string Title { get; }

    public ReviewWorkflowStep Step { get; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
