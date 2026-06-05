using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;
using Ryn.Ipc;

namespace Ryn.Plugins.FileSystem;

public static class FileSystemServiceCollectionExtensions
{
    public static IServiceCollection AddRynFileSystem(this IServiceCollection services, Action<FileSystemOptions>? configure = null)
    {
        var options = new FileSystemOptions();
        configure?.Invoke(options);
        services.AddSingleton(sp =>
        {
            var caps = sp.GetService<RynCapabilities>();
            if (caps is not null)
                CapabilityScopeMerger.MergeFileSystemScope(caps, options);
            return options;
        });
        // Per-application validator (and, transitively, the command instance the generated router resolves):
        // each app's DI container holds its own, so multiple apps in one process don't share one global policy.
        services.AddSingleton<PathValidator>();
        services.AddSingleton<FileSystemPlugin>();
        services.AddSingleton<IRynPlugin>(sp => sp.GetRequiredService<FileSystemPlugin>());
        services.AddFileSystemCommands(); // generated
        return services;
    }
}
