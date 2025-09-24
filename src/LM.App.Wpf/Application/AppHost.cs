using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Application
{
    internal sealed class AppHost : IAsyncDisposable, IDisposable
    {
        private readonly IHost _host;

        public AppHost(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public IServiceProvider Services => _host.Services;

        public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();

        public Task StartAsync(CancellationToken ct = default) => _host.StartAsync(ct);

        public Task StopAsync(CancellationToken ct = default) => _host.StopAsync(ct);

        public void Dispose() => _host.Dispose();

        public ValueTask DisposeAsync()
        {
            if (_host is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            _host.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
