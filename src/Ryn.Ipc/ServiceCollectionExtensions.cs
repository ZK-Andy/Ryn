using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;

namespace Ryn.Ipc;

public static class RynIpcServiceCollectionExtensions
{
    public static IServiceCollection AddRynCommands(this IServiceCollection services)
    {
        services.AddSingleton(_ => RynCapabilitiesLoader.Load());
        services.AddSingleton<IIpcObserver, LoggingIpcObserver>();
        services.AddSingleton<RynCommandDispatcher>();
        services.AddSingleton<CommandDispatchHandler>(sp =>
            sp.GetRequiredService<RynCommandDispatcher>().DispatchAsync);
        services.AddSingleton<WindowCommands>();
        services.AddWindowCommands();
        return services;
    }
}
