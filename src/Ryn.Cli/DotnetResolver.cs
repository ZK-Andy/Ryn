using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Ryn.Cli;

/// <summary>
/// Locates a working <c>dotnet</c> executable, even when it is not on <c>PATH</c>.
/// Candidates are tried in order — <c>DOTNET_HOST_PATH</c>, <c>PATH</c>, the <c>DOTNET_ROOT</c>
/// family, then well-known per-platform install locations — and each is validated (it must be a
/// real executable file, and it must actually run <c>--version</c>) so broken entries such as a
/// dangling <c>/usr/local/bin/dotnet</c> symlink are skipped rather than returned. The result is
/// cached for the process lifetime.
/// </summary>
internal static class DotnetResolver
{
    private static string? _cached;
    private static bool _resolved;

    private static string ExeName => OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

    /// <summary>
    /// Returns the full path to a usable dotnet executable, or <see langword="null"/> if none can be found.
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
            Console.Error.WriteLine("Error: could not find a working 'dotnet' executable.");
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
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Prefer a candidate that actually runs `--version`. If none verifies (e.g. an SDK is
        // present but broken), fall back to the first executable so the command that needs dotnet
        // can surface the real error rather than a misleading "not found".
        string? firstExecutable = null;

        foreach (var candidate in EnumerateCandidates())
        {
            if (!seen.Add(candidate) || !IsExecutableFile(candidate))
                continue;

            firstExecutable ??= candidate;
            if (RunsSuccessfully(candidate))
                return candidate;
        }

        return firstExecutable;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        // 1. DOTNET_HOST_PATH — set by the SDK/host; points directly at the dotnet executable.
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(hostPath))
            yield return hostPath;

        // 2. Every PATH entry (a broken match no longer stops the search).
        foreach (var candidate in PathCandidates())
            yield return candidate;

        // 3. DOTNET_ROOT family — a directory that contains the dotnet executable.
        foreach (var envVar in (string[])["DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_ROOT_X64"])
        {
            var root = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(root))
                continue;

            var candidate = TryCombine(root, ExeName);
            if (candidate is not null)
                yield return candidate;
        }

        // 4. Well-known install locations.
        foreach (var candidate in WellKnownPaths())
            yield return candidate;
    }

    private static IEnumerable<string> PathCandidates()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            yield break;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = TryCombine(dir, ExeName);
            if (candidate is not null)
                yield return candidate;
        }
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

    private static string? TryCombine(string dir, string file)
    {
        try
        {
            return Path.Combine(dir, file);
        }
        catch (ArgumentException)
        {
            return null; // a directory with invalid path characters
        }
    }

    /// <summary>
    /// True if the path is a real, executable file. Follows symlinks, so a dangling link (whose
    /// target is missing) is rejected, and on Unix an execute bit is required.
    /// </summary>
    private static bool IsExecutableFile([NotNullWhen(true)] string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            if (OperatingSystem.IsWindows())
                return File.Exists(path);

            // GetUnixFileMode follows symlinks, so a dangling link throws (and is rejected here).
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (mode & anyExecute) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Runs <c>{dotnet} --version</c> and returns true only if it exits 0.</summary>
    private static bool RunsSuccessfully(string dotnetPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process is null)
                return false;

            if (!process.WaitForExit(milliseconds: 15_000))
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* already exited */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
