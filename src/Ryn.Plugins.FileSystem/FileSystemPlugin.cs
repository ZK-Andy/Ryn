using Ryn.Core;

namespace Ryn.Plugins.FileSystem;

public sealed class FileSystemPlugin : IRynPlugin
{
    private readonly FileSystemOptions _options;

    public FileSystemPlugin(FileSystemOptions options)
    {
        _options = options;
    }

    public string Name => "FileSystem";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        PathValidator.Configure(_options);
        return ValueTask.CompletedTask;
    }
}
