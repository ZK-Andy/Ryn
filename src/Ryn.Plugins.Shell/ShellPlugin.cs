using Ryn.Core;
using Ryn.Interop;

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
        NativeLibraryResolver.RegisterForAssembly(typeof(ShellPlugin).Assembly);
        return ValueTask.CompletedTask;
    }
}
