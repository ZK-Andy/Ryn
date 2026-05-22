using System.IO;

namespace Ryn.Plugins.FileSystem;

internal static class PathValidator
{
    private static FileSystemOptions? _options;

    internal static void Configure(FileSystemOptions options) => _options = options;

    internal static string Resolve(string path)
    {
        var fullPath = Path.GetFullPath(path);

        // Check for path traversal
        if (path.Contains("..", StringComparison.Ordinal))
        {
            var normalized = Path.GetFullPath(path);
            if (normalized != fullPath)
                throw new UnauthorizedAccessException($"Path traversal detected: {path}");
        }

        var options = _options;
        if (options is null || options.AllowedPaths.Count == 0)
        {
            // Default: restrict to app directory
            var appDir = AppContext.BaseDirectory;
            if (!fullPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access denied: path is outside the application directory");
            return fullPath;
        }

        foreach (var allowed in options.AllowedPaths)
        {
            var allowedFull = Path.GetFullPath(allowed);
            if (fullPath.StartsWith(allowedFull, StringComparison.OrdinalIgnoreCase))
                return fullPath;
        }

        throw new UnauthorizedAccessException($"Access denied: path is not within any allowed directory");
    }
}
