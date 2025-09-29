#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.Views.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.Library
{
    internal sealed class PdfViewerLauncher : IPdfViewerLauncher
    {
        private readonly IServiceProvider _services;
        private readonly IWorkSpaceService _workspace;

        public PdfViewerLauncher(IServiceProvider services, IWorkSpaceService workspace)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(workspace);

            _services = services;
            _workspace = workspace;
        }

        public Task<bool> LaunchAsync(Entry entry, string? attachmentId = null)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                throw new InvalidOperationException("Application dispatcher is not available.");
            }

            if (!dispatcher.CheckAccess())
            {
                var operation = dispatcher.InvokeAsync(() => LaunchInternalAsync(entry, attachmentId));
                return operation.Task.Unwrap();
            }

            return LaunchInternalAsync(entry, attachmentId);
        }

        private async Task<bool> LaunchInternalAsync(Entry entry, string? attachmentId)
        {
            var relativePath = ResolveRelativePath(entry, attachmentId);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var absolutePath = _workspace.GetAbsolutePath(relativePath);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return false;
            }

            if (!string.Equals(Path.GetExtension(absolutePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var scope = _services.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<PdfViewerViewModel>();
            var initialized = await viewModel.InitializeAsync(entry, absolutePath, attachmentId).ConfigureAwait(true);
            if (!initialized)
            {
                return false;
            }

            var window = new PdfViewerWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = viewModel
            };

            window.Show();
            return true;
        }

        private static string? ResolveRelativePath(Entry entry, string? attachmentId)
        {
            if (!string.IsNullOrWhiteSpace(attachmentId))
            {
                return entry.Attachments.FirstOrDefault(a => a.Id == attachmentId)?.RelativePath;
            }

            return entry.MainFilePath;
        }
    }
}
