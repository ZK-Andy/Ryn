using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;

namespace Ryn.Plugins.Shell;

public static class ShellServiceCollectionExtensions
{
    public static IServiceCollection AddRynShell(this IServiceCollection services, Action<ShellOptions>? configure = null)
    {
        var options = new ShellOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<ShellPlugin>();
        services.AddSingleton<IRynPlugin>(sp => sp.GetRequiredService<ShellPlugin>());
        services.AddShellCommands(); // generated
        return services;
    }
}
