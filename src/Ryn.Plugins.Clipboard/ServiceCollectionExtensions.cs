using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;

namespace Ryn.Plugins.Clipboard;

public static class ClipboardServiceCollectionExtensions
{
    public static IServiceCollection AddRynClipboard(this IServiceCollection services)
    {
        services.AddSingleton<ClipboardPlugin>();
        services.AddSingleton<IRynPlugin>(sp => sp.GetRequiredService<ClipboardPlugin>());
        services.AddClipboardCommands(); // generated
        return services;
    }
}
