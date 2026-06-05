using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;
using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

public static class ShellServiceCollectionExtensions
{
    public static IServiceCollection AddRynShell(this IServiceCollection services, Action<ShellOptions>? configure = null)
    {
        var options = new ShellOptions();
        configure?.Invoke(options);
        services.AddSingleton(sp =>
        {
            var caps = sp.GetService<RynCapabilities>();
            if (caps is not null)
                CapabilityScopeMerger.MergeShellScope(caps, options);
            return options;
        });
        // Per-application execution policy (allowlist + argument/process rules). Each app's DI container
        // holds its own, so multiple apps in one process don't share one global shell policy.
        services.AddSingleton<ShellExecutionPolicy>();
        services.AddSingleton<ShellPlugin>();
        services.AddSingleton<IRynPlugin>(sp => sp.GetRequiredService<ShellPlugin>());
        services.AddShellCommands(); // generated
        services.AddSpawnCommands(); // generated
        services.AddPtyCommands(); // generated
        return services;
    }
}
