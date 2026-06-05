using Ryn.Core;
using Ryn.Interop;

namespace Ryn.Plugins.Shell;

public sealed class ShellPlugin : IRynPlugin
{
    private readonly ShellExecutionPolicy _policy;

    public ShellPlugin(ShellExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public string Name => "Shell";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Touch the policy so it (and therefore ShellOptions, with its ryn.json capability merge) is
        // resolved at startup rather than lazily on the first shell command. Configuration now lives on this
        // per-application instance instead of the former process-global ShellCommands static.
        _ = _policy;
        NativeLibraryResolver.RegisterForAssembly(typeof(ShellPlugin).Assembly);
        return ValueTask.CompletedTask;
    }
}
