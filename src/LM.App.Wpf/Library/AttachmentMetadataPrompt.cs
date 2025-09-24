#nullable enable
using System;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.Library;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.Library
{
    internal sealed class AttachmentMetadataPrompt : IAttachmentMetadataPrompt
    {
        private readonly IServiceProvider _services;

        public AttachmentMetadataPrompt(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public Task<AttachmentMetadataPromptResult?> RequestMetadataAsync(AttachmentMetadataPromptContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var dispatcher = System.Windows.Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("Application dispatcher is not available.");

            if (!dispatcher.CheckAccess())
            {
                var operation = dispatcher.InvokeAsync(() => ShowDialogAsync(context));
                return operation.Task.Unwrap();
            }

            return ShowDialogAsync(context);
        }

        private Task<AttachmentMetadataPromptResult?> ShowDialogAsync(AttachmentMetadataPromptContext context)
        {
            using var scope = _services.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<AttachmentMetadataDialogViewModel>();
            viewModel.Initialize(context);

            var window = new Views.Library.AttachmentMetadataDialog(viewModel)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            var result = window.ShowDialog();
            return Task.FromResult(result == true ? viewModel.BuildResult() : null);
        }
    }
}
