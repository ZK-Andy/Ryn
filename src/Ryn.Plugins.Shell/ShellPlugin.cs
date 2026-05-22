using Ryn.Core;

namespace Ryn.Plugins.Shell;

public sealed class ShellPlugin : IRynPlugin
{
    private readonly ShellOptions _options;

    public ShellPlugin(ShellOptions options)
    {
        _options = options;
    }

    public string Name => "Shell";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ShellCommands.Configure(_options);
        return ValueTask.CompletedTask;
    }
}
