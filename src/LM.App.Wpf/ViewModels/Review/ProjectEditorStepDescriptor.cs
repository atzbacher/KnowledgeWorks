#nullable enable

namespace LM.App.Wpf.ViewModels.Review;

public sealed record ProjectEditorStepDescriptor(
    ProjectEditorStep Step,
    string Title,
    string Description);
