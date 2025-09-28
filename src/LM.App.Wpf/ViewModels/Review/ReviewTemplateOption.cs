#nullable enable
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review;

public sealed record ReviewTemplateOption(
    ReviewTemplateKind Kind,
    string Title,
    string Description);
