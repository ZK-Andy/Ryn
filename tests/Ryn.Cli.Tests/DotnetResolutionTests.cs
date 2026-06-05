using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Ryn.Cli.Tests;

/// <summary>
/// Regression coverage for the CLI's internal dotnet resolution: the built `ryn` binary must
/// still locate dotnet when it is installed but NOT on PATH (resolving via DOTNET_ROOT).
/// Unlike the other CLI tests, this drives the compiled apphost directly rather than via
/// `dotnet run`, so it can run with a PATH that contains no dotnet at all.
/// </summary>
public sealed class DotnetResolutionTests
{
    // Build the apphost once for the class; Lazy is thread-safe.
    private static readonly Lazy<string> AppHost = new(BuildCliAppHost);

    [Fact]
    public async Task Doctor_ResolvesDotnet_ViaDotnetRoot_WhenNotOnPath()
    {
        var dotnetExe = RealDotnetPath();
        var dotnetDir = Path.GetDirectoryName(dotnetExe)!;

        // Remove every PATH entry that contains a dotnet executable, so the resolver cannot
        // fall back to PATH and MUST use DOTNET_ROOT.
        var sanitizedPath = string.Join(
            Path.PathSeparator,
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(dir => !DirectoryContainsDotnet(dir)));

        // Sanity: PATH really had no dotnet for the child process.
        DotnetIsOnPath(sanitizedPath).Should().BeFalse("the test must strip dotnet from PATH");

        var psi = new ProcessStartInfo
        {
            FileName = AppHost.Value,
            // Run from a temp dir so other doctor checks (native libs) don't matter here.
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("doctor");
        psi.Environment["PATH"] = sanitizedPath;
        psi.Environment.Remove("DOTNET_HOST_PATH"); // checked before DOTNET_ROOT; clear to isolate the branch
        // DOTNET_ROOT both lets the apphost find its own runtime and is the branch under test.
        psi.Environment["DOTNET_ROOT"] = dotnetDir;

#pragma warning disable CA2007 // xUnit (xUnit1030) forbids ConfigureAwait(false) in test methods
        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
#pragma warning restore CA2007

        var because = $"the resolver should find dotnet via DOTNET_ROOT.\nstdout:\n{stdout}\nstderr:\n{stderr}";

        stdout.Should().Contain("Ryn Doctor", because);
        stdout.Should().NotContain("dotnet not found", because);
        // doctor prints e.g. "[ OK ] .NET SDK — 10.0.300"
        stdout.Should().MatchRegex(@"\.NET SDK[^\n]*10\.", because);
    }

    [Fact]
    public async Task Doctor_SkipsBrokenDotnetSymlink_OnPath()
    {
        // Mirrors the real /usr/local/bin/dotnet -> (missing target) dangling symlink: a PATH entry
        // whose `dotnet` exists as a name but is not runnable must be skipped, not returned.
        if (OperatingSystem.IsWindows())
            return; // POSIX symlink semantics; Windows symlink creation needs elevation.

        var dotnetDir = Path.GetDirectoryName(RealDotnetPath())!;

        var brokenDir = Path.Combine(Path.GetTempPath(), "ryn-broken-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(brokenDir);
        try
        {
            File.CreateSymbolicLink(
                Path.Combine(brokenDir, DotnetExeName),
                Path.Combine(brokenDir, "missing-target"));

            // PATH = broken dir first, then every real-dotnet dir stripped out. The only working
            // dotnet is reachable via DOTNET_ROOT, so the resolver must skip the broken link to find it.
            var sanitized = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(dir => !DirectoryContainsDotnet(dir));
            var path = string.Join(Path.PathSeparator, new[] { brokenDir }.Concat(sanitized));

            var psi = new ProcessStartInfo
            {
                FileName = AppHost.Value,
                WorkingDirectory = Path.GetTempPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("doctor");
            psi.Environment["PATH"] = path;
            psi.Environment.Remove("DOTNET_HOST_PATH");
            psi.Environment["DOTNET_ROOT"] = dotnetDir;

#pragma warning disable CA2007 // xUnit (xUnit1030) forbids ConfigureAwait(false) in test methods
            using var process = Process.Start(psi)!;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
#pragma warning restore CA2007

            var because = $"the resolver must skip the broken symlink and resolve via DOTNET_ROOT.\nstdout:\n{stdout}\nstderr:\n{stderr}";

            stdout.Should().NotContain("'--version' failed", because); // would mean the broken link was returned
            stdout.Should().MatchRegex(@"\.NET SDK[^\n]*10\.", because);
        }
        finally
        {
            Directory.Delete(brokenDir, recursive: true);
        }
    }

    private static bool DirectoryContainsDotnet(string dir)
    {
        try
        {
            return File.Exists(Path.Combine(dir, DotnetExeName));
        }
        catch (ArgumentException)
        {
            return false; // PATH entry with invalid path characters
        }
    }

    private static bool DotnetIsOnPath(string path) =>
        path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(DirectoryContainsDotnet);

    private static string DotnetExeName => OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

    /// <summary>Finds the real (symlink-resolved) dotnet the test environment is using.</summary>
    private static string RealDotnetPath()
    {
        string? found = null;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (DirectoryContainsDotnet(dir))
            {
                found = Path.Combine(dir, DotnetExeName);
                break;
            }
        }

        if (found is null)
        {
            var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(hostPath) && File.Exists(hostPath))
                found = hostPath;
        }

        if (found is null)
        {
            var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, DotnetExeName)))
                found = Path.Combine(root, DotnetExeName);
        }

        if (found is null)
            throw new InvalidOperationException("Could not locate dotnet to set up the test environment.");

        // Resolve symlinks so DOTNET_ROOT points at the real install dir (with shared/ and sdk/).
        var target = new FileInfo(found).ResolveLinkTarget(returnFinalTarget: true);
        return target?.FullName ?? Path.GetFullPath(found);
    }

    private static string BuildCliAppHost()
    {
        var repoRoot = FindRepoRoot();
        var csproj = Path.Combine(repoRoot, "src", "Ryn.Cli", "Ryn.Cli.csproj");
        var config = BuildConfiguration();
        var appHost = Path.Combine(repoRoot, "src", "Ryn.Cli", "bin", config, "net10.0", DotnetExeName == "dotnet.exe" ? "Ryn.Cli.exe" : "Ryn.Cli");

        if (!File.Exists(appHost))
        {
            var psi = new ProcessStartInfo
            {
                FileName = RealDotnetPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("build");
            psi.ArgumentList.Add(csproj);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(config);
            psi.ArgumentList.Add("--nologo");

            using var process = Process.Start(psi)!;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to build the Ryn.Cli apphost for the test:\n{stdoutTask.GetAwaiter().GetResult()}{stderrTask.GetAwaiter().GetResult()}");
            }
        }

        File.Exists(appHost).Should().BeTrue("the Ryn.Cli apphost should exist after building");
        return appHost;
    }

    private static string BuildConfiguration()
    {
        // The test assembly lives in .../bin/<Config>/net10.0/.
        var tfmDir = Path.GetDirectoryName(typeof(DotnetResolutionTests).Assembly.Location)!;
        return Path.GetFileName(Path.GetDirectoryName(tfmDir)!);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Ryn.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Ryn repository root (Ryn.slnx).");
    }
}
