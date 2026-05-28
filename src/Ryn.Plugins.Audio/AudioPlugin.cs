using Ryn.Core;

namespace Ryn.Plugins.Audio;

#pragma warning disable CA1812 // Instantiated by DI
internal sealed class AudioPlugin : IRynPlugin
#pragma warning restore CA1812
{
    public string Name => "audio";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
