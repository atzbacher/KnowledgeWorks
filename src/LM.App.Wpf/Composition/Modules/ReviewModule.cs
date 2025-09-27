#nullable enable
using System;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Services;
using LM.App.Wpf.Services.Review;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.ViewModels.Review;
using LM.App.Wpf.Views.Review;
using LM.Infrastructure.Review;
using LM.Review.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition.Modules;

internal sealed class ReviewModule : IAppModule
{
    public void ConfigureServices(HostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<WorkspaceReviewWorkflowStore>();
        services.AddSingleton<IReviewWorkflowStore>(static sp => sp.GetRequiredService<WorkspaceReviewWorkflowStore>());
        services.AddSingleton<IReviewHookContextFactory, ReviewHookContextFactory>();
        services.AddSingleton<IReviewWorkflowService>(static sp => new ReviewWorkflowService(
            sp.GetRequiredService<IReviewWorkflowStore>(),
            sp.GetRequiredService<IReviewHookOrchestrator>(),
            sp.GetRequiredService<IReviewHookContextFactory>()));
        services.AddSingleton<IReviewAnalyticsService, ReviewAnalyticsService>();

        services.AddSingleton<IUserContext, UserContext>();

        services.AddTransient<ILitSearchRunPicker, LitSearchRunPicker>();
        services.AddSingleton<IMessageBoxService, WpfMessageBoxService>();
        services.AddTransient<IReviewProjectLauncher, ReviewProjectLauncher>();
        services.AddTransient<LitSearchRunPickerViewModel>();
        services.AddTransient<LitSearchRunPickerWindow>();


        services.AddTransient<ProjectDashboardViewModel>();
        services.AddTransient<ScreeningQueueViewModel>();
        services.AddTransient<AssignmentDetailViewModel>();
        services.AddTransient<ExtractionWorkspaceViewModel>();
        services.AddTransient<QualityAssuranceViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<ReviewViewModel>();

        services.AddTransient<Func<ReviewViewModel>>(static sp => () => sp.GetRequiredService<ReviewViewModel>());
    }
}
