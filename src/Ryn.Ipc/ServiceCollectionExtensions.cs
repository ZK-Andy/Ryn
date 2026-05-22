using Microsoft.Extensions.DependencyInjection;

namespace Ryn.Ipc;

public static class RynIpcServiceCollectionExtensions
{
    public static IServiceCollection AddRynCommands(this IServiceCollection services)
    {
        services.AddSingleton<RynCommandDispatcher>();
        return services;
    }
}
