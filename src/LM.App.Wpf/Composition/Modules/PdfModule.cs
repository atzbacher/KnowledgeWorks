using LM.App.Wpf.ViewModels.Pdf;
using LM.App.Wpf.Views;
using LM.Core.Abstractions;
using LM.Infrastructure.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition.Modules
{
    internal sealed class PdfModule : IAppModule
    {
        public void ConfigureServices(HostApplicationBuilder builder)
        {
            var services = builder.Services;

            services.AddSingleton<IPdfAnnotationPersistenceService, PdfAnnotationPersistenceService>();
            services.AddSingleton<PdfViewerViewModel>();
            services.AddTransient<PdfViewer>(sp =>
            {
                var view = new PdfViewer
                {
                    DataContext = sp.GetRequiredService<PdfViewerViewModel>()
                };
                return view;
            });
        }
    }
}
