using Ryn.Core;

namespace Ryn.Plugins.FileSystem;

public sealed class FileSystemPlugin : IRynPlugin
{
    private readonly PathValidator _validator;

    public FileSystemPlugin(PathValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    public string Name => "FileSystem";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Touch the validator so it (and therefore FileSystemOptions, with its ryn.json capability merge)
        // is resolved at startup rather than lazily on the first filesystem command. Configuration now lives
        // on this per-application instance instead of the former process-global PathValidator static.
        _ = _validator;
        return ValueTask.CompletedTask;
    }
}
