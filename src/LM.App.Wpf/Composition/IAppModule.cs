using Microsoft.Extensions.Hosting;

namespace LM.App.Wpf.Composition
{
    internal interface IAppModule
    {
        void ConfigureServices(HostApplicationBuilder builder);
    }
}
