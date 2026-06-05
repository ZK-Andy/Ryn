using System.Diagnostics.CodeAnalysis;

namespace Ryn.Cli;

/// <summary>
/// Locates the <c>dotnet</c> executable, even when it is not on <c>PATH</c>.
/// Resolution order: <c>DOTNET_HOST_PATH</c>, <c>PATH</c>, the <c>DOTNET_ROOT</c> family,
/// then well-known per-platform install locations. The result is cached for the process lifetime.
/// </summary>
internal static class DotnetResolver
{
    private static string? _cached;
    private static bool _resolved;

    private static string ExeName => OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

    /// <summary>
    /// Returns the full path to the dotnet executable, or <see langword="null"/> if it cannot be found.
    /// </summary>
    internal static string? Resolve()
    {
        if (_resolved)
            return _cached;

        _cached = ResolveCore();
        _resolved = true;
        return _cached;
    }

    /// <summary>
    /// Resolves dotnet or, if it cannot be found, prints an actionable error to stderr and returns
    /// <see langword="null"/>. Commands should return a non-zero exit code when this returns null.
    /// </summary>
    internal static string? ResolveOrReport()
    {
        var path = Resolve();
        if (path is null)
        {
            Console.Error.WriteLine("Error: could not find the 'dotnet' executable.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Ryn needs the .NET SDK to build your project. Either:");
            Console.Error.WriteLine("  - Install the .NET 10 SDK: https://dotnet.microsoft.com/download");
            Console.Error.WriteLine("  - Or, if .NET is already installed, add it to your PATH, or set the");
            Console.Error.WriteLine("    DOTNET_ROOT environment variable to its install directory.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Run 'ryn doctor' to diagnose your environment.");
        }

        return path;
    }

    private static string? ResolveCore()
    {
        // 1. DOTNET_HOST_PATH — set by the SDK/host; points directly at the dotnet executable.
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (IsExecutableFile(hostPath))
            return hostPath;

        // 2. PATH lookup. Resolving to a full path (rather than relying on Process.Start's own
        //    search) keeps behavior identical across platforms and lets the muxer derive its root.
        if (FindOnPath(ExeName) is { } onPath)
            return onPath;

        // 3. DOTNET_ROOT family — a directory that contains the dotnet executable.
        foreach (var envVar in (ReadOnlySpan<string>)["DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_ROOT_X64"])
        {
            var root = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(root))
                continue;

            var candidate = Path.Combine(root, ExeName);
            if (IsExecutableFile(candidate))
                return candidate;
        }

        // 4. Well-known install locations.
        foreach (var candidate in WellKnownPaths())
        {
            if (IsExecutableFile(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> WellKnownPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var envVar in (string[])["ProgramFiles", "ProgramFiles(x86)", "ProgramW6432"])
            {
                var root = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(root))
                    yield return Path.Combine(root, "dotnet", "dotnet.exe");
            }

            var localAppData = Environment.GetEnvironmentVariable("LocalAppData");
            if (!string.IsNullOrEmpty(localAppData))
                yield return Path.Combine(localAppData, "Microsoft", "dotnet", "dotnet.exe");
        }
        else
        {
            // macOS (arm64 + the x64 SDK installed alongside it), and common Linux locations.
            yield return "/usr/local/share/dotnet/dotnet";
            if (OperatingSystem.IsMacOS())
                yield return "/usr/local/share/dotnet/x64/dotnet";
            yield return "/usr/share/dotnet/dotnet";
            yield return "/usr/lib/dotnet/dotnet";
            yield return "/opt/homebrew/bin/dotnet";
            yield return "/opt/homebrew/share/dotnet/dotnet";
            yield return "/snap/dotnet-sdk/current/dotnet";

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                yield return Path.Combine(home, ".dotnet", "dotnet");
        }
    }

    private static string? FindOnPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(dir, exeName);
            }
            catch (ArgumentException)
            {
                continue; // a PATH entry with invalid path characters
            }

            if (IsExecutableFile(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsExecutableFile([NotNullWhen(true)] string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path);
}
