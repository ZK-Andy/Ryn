using Microsoft.Extensions.DependencyInjection;

namespace Ryn.Plugins.Clipboard;

public static class ClipboardServiceCollectionExtensions
{
    public static IServiceCollection AddRynClipboard(this IServiceCollection services)
    {
        services.AddSingleton<ClipboardPlugin>();
        services.AddClipboardCommands(); // generated
        return services;
    }
}
