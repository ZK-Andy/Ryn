using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;

namespace Ryn.Plugins.Updater;

public static class UpdaterServiceCollectionExtensions
{
    public static IServiceCollection AddRynUpdater(this IServiceCollection services, Action<UpdaterOptions>? configure = null)
    {
        var options = new UpdaterOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<UpdaterService>();
        services.AddSingleton<UpdaterCommands>();
        services.AddSingleton<IRynPlugin, UpdaterPlugin>();
        services.AddUpdaterCommands(); // generated

        return services;
    }
}
