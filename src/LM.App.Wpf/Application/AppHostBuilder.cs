using System;
using System.Collections.Generic;
using LM.App.Wpf.Composition;
using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Application
{
    internal sealed class AppHostBuilder
    {
        private readonly HostApplicationBuilder _builder;
        private readonly List<IAppModule> _modules = new();

        private AppHostBuilder(HostApplicationBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public static AppHostBuilder Create() => new(new HostApplicationBuilder());

        public AppHostBuilder Configure(Action<HostApplicationBuilder>? configure)
        {
            configure?.Invoke(_builder);
            return this;
        }

        public AppHostBuilder AddModule(IAppModule module)
        {
            if (module is null) throw new ArgumentNullException(nameof(module));
            _modules.Add(module);
            return this;
        }

        public AppHost Build()
        {
            foreach (var module in _modules)
            {
                module.ConfigureServices(_builder);
            }

            var host = _builder.Build();
            return new AppHost(host);
        }
    }
}
