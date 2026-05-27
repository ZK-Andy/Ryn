using Microsoft.Extensions.DependencyInjection;

namespace Ryn.Plugins.Shell;

public static class ShellServiceCollectionExtensions
{
    public static IServiceCollection AddRynShell(this IServiceCollection services, Action<ShellOptions>? configure = null)
    {
        var options = new ShellOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<ShellPlugin>();
        services.AddShellCommands(); // generated
        services.AddSpawnCommands(); // generated
        return services;
    }
}
