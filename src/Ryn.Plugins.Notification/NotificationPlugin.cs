using Ryn.Core;

namespace Ryn.Plugins.Notification;

public sealed class NotificationPlugin : IRynPlugin
{
    public string Name => "Notification";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
