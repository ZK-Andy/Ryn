using Microsoft.Extensions.DependencyInjection;

namespace Ryn.Plugins.Notification;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddRynNotification(this IServiceCollection services)
    {
        services.AddSingleton<NotificationPlugin>();
        services.AddNotificationCommands(); // generated
        return services;
    }
}
