using Ryn.Core;

namespace Ryn.Plugins.Tray;

#pragma warning disable CA1812 // Instantiated by DI
internal sealed class TrayPlugin : IRynPlugin
#pragma warning restore CA1812
{
    public string Name => "tray";

    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
