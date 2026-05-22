using Ryn.Core;

namespace Ryn.Plugins.Clipboard;

public sealed class ClipboardPlugin : IRynPlugin
{
    public string Name => "Clipboard";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
