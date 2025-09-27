namespace LM.App.Wpf.ViewModels.Review;

using System;
using LM.Review.Core.Models;

internal sealed class ReviewWorkflowCompletedEventArgs : EventArgs
{
    public ReviewWorkflowCompletedEventArgs(ReviewProjectDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public ReviewProjectDefinition Definition { get; }
}
