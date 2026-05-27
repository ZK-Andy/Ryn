namespace Ryn.Plugins.FileSystem;

internal static class PathValidator
{
    private static FileSystemOptions? _options;

    internal static void Configure(FileSystemOptions options) => _options = options;

    internal static string Resolve(string path)
    {
        // Resolve relative paths against the app's base directory, not CWD
        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

        var options = _options;
        if (options is not null && options.AccessDenied)
            throw new UnauthorizedAccessException("File system access is denied by capability policy");

        if (options is null || options.AllowedPaths.Count == 0)
        {
            // Default: restrict to app directory
            if (!IsWithin(fullPath, AppContext.BaseDirectory))
                throw new UnauthorizedAccessException($"Access denied: path '{path}' is outside the application directory");
            return fullPath;
        }

        foreach (var allowed in options.AllowedPaths)
        {
            if (IsWithin(fullPath, allowed))
                return fullPath;
        }

        throw new UnauthorizedAccessException($"Access denied: path '{path}' is not within any allowed directory");
    }

    private static bool IsWithin(string fullPath, string directory)
    {
        // Normalize both to remove trailing slashes, then compare with separator
        var normalizedDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedPath.Equals(normalizedDir, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
