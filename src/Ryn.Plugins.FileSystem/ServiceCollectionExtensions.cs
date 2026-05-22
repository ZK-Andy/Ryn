using Microsoft.Extensions.DependencyInjection;

namespace Ryn.Plugins.FileSystem;

public static class FileSystemServiceCollectionExtensions
{
    public static IServiceCollection AddRynFileSystem(this IServiceCollection services, Action<FileSystemOptions>? configure = null)
    {
        var options = new FileSystemOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<FileSystemPlugin>();
        services.AddFileSystemCommands(); // generated
        return services;
    }
}
