#nullable enable
using System;
using LM.App.Wpf.ViewModels.Review;
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

        services.AddTransient<ReviewDashboardViewModel>();
        services.AddTransient<ReviewStageViewModel>();

        services.AddTransient<Func<ReviewDashboardViewModel>>(static sp => () => sp.GetRequiredService<ReviewDashboardViewModel>());
        services.AddTransient<Func<ReviewStageViewModel>>(static sp => () => sp.GetRequiredService<ReviewStageViewModel>());
    }
}
