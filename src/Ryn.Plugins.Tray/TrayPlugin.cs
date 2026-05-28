using Ryn.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Ryn.Plugins.Tray;

#pragma warning disable CA1812 // Instantiated by DI
internal sealed class TrayPlugin : IRynPlugin
#pragma warning restore CA1812
{
    private readonly IServiceProvider _services;

    public TrayPlugin(IServiceProvider services) => _services = services;

    public string Name => "tray";

    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        var trayService = _services.GetRequiredService<TrayService>();
        trayService.EmitEvent = (eventName, jsonData) =>
        {
            var webView = _services.GetService<IRynWebView>();
            webView?.EmitEvent(eventName, jsonData);
        };
        return ValueTask.CompletedTask;
    }
}
