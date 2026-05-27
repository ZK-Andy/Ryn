namespace Ryn.Plugins.FileSystem;

public sealed class FileSystemOptions
{
    public List<string> AllowedPaths { get; set; } = [];
    internal bool AccessDenied { get; set; }
}
