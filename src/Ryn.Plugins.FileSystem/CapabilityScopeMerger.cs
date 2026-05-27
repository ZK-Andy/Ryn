using Ryn.Ipc;

namespace Ryn.Plugins.FileSystem;

internal static class CapabilityScopeMerger
{
    /// <summary>
    /// Merges capability scopes from ryn.json into FileSystemOptions.
    /// If ryn.json declares path scopes, they become the maximum allowed set.
    /// Programmatic paths that fall outside the declared scopes are removed.
    /// If ryn.json doesn't declare scopes, programmatic options apply as-is.
    /// </summary>
    internal static void MergeFileSystemScope(RynCapabilities capabilities, FileSystemOptions options)
    {
        var scope = capabilities.GetScope("fs");
        if (scope is null || !scope.HasPathPolicy)
            return;

        // scope: [] means explicit deny-all
        if (scope.AllowedPaths!.Count == 0)
        {
            options.AllowedPaths.Clear();
            options.AccessDenied = true;
            return;
        }

        if (options.AllowedPaths.Count == 0)
        {
            options.AllowedPaths.AddRange(scope.AllowedPaths);
            return;
        }

        var clamped = new List<string>();
        foreach (var programmatic in options.AllowedPaths)
        {
            var resolved = Path.GetFullPath(programmatic);
            foreach (var allowed in scope.AllowedPaths)
            {
                if (IsWithin(resolved, allowed))
                {
                    clamped.Add(programmatic);
                    break;
                }
            }
        }

        options.AllowedPaths.Clear();
        if (clamped.Count == 0)
            options.AccessDenied = true;
        else
            options.AllowedPaths.AddRange(clamped);
    }

    private static bool IsWithin(string fullPath, string directory)
    {
        var normalizedDir = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = fullPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedPath.Equals(normalizedDir, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
