using Ryn.Core;

namespace Ryn.Plugins.Dialog;

public sealed class DialogPlugin : IRynPlugin
{
    public string Name => "Dialog";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
