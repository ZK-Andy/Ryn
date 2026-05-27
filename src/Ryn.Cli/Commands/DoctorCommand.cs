using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ryn.Cli.Commands;

internal static class DoctorCommand
{
    internal static int Execute(ReadOnlySpan<string> args)
    {
        Console.WriteLine("Ryn Doctor");
        Console.WriteLine("==========");
        Console.WriteLine();

        var allGood = true;

        allGood &= Check(".NET SDK", CheckDotNetSdk);
        allGood &= Check("Native libraries", CheckNativeLibs);
        allGood &= Check("WebView runtime", CheckWebView);
        allGood &= Check("GitHub CLI (gh)", CheckGitHubCli);
        allGood &= Check("CMake", CheckCmake);
        allGood &= Check("Ninja", CheckNinja);

        if (args.Contains("--full"))
        {
            allGood &= Check("Solution build", CheckSolutionBuild);
        }

        Console.WriteLine();
        if (allGood)
            Console.WriteLine("All checks passed!");
        else
            Console.WriteLine("Some checks failed. See above for details.");

        return allGood ? 0 : 1;
    }

    private static bool Check(string label, Func<CheckResult> check)
    {
        var result = check();

        var (color, tag) = result.Status switch
        {
            CheckStatus.Ok => (ConsoleColor.Green, " OK "),
            CheckStatus.Warn => (ConsoleColor.Yellow, "WARN"),
            CheckStatus.Fail => (ConsoleColor.Red, "FAIL"),
            _ => (ConsoleColor.White, "????"),
        };

        var saved = Console.ForegroundColor;
        Console.Write("  [");
        Console.ForegroundColor = color;
        Console.Write(tag);
        Console.ForegroundColor = saved;
        Console.Write("] ");
        Console.Write(label);

        if (!string.IsNullOrEmpty(result.Detail))
        {
            Console.Write(" — ");
            Console.Write(result.Detail);
        }

        Console.WriteLine();

        return result.Status != CheckStatus.Fail;
    }

    private static CheckResult CheckDotNetSdk()
    {
        var version = RunCommand("dotnet", "--version");
        if (version is null)
            return CheckResult.Fail("dotnet not found on PATH");

        version = version.Trim();
        if (version.StartsWith("10.", StringComparison.Ordinal))
            return CheckResult.Ok(version);

        return CheckResult.Fail($"found {version}, need 10.x");
    }

    private static CheckResult CheckNativeLibs()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var interopDir = FindInteropDir();
        if (interopDir is null)
            return CheckResult.Fail("cannot locate src/Ryn.Interop");

        var nativeDir = Path.Combine(interopDir, "runtimes", rid, "native");
        if (!Directory.Exists(nativeDir))
            return CheckResult.Fail($"no native libs for {rid} (expected {nativeDir})");

        var files = Directory.GetFiles(nativeDir);
        if (files.Length == 0)
            return CheckResult.Fail($"native dir exists but is empty for {rid}");

        return CheckResult.Ok($"{files.Length} file(s) for {rid}");
    }

    private static CheckResult CheckWebView()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return CheckResult.Ok("WebKit (built into macOS)");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CheckWebView2();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CheckWebKitGtk();

        return CheckResult.Warn("unknown platform");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static CheckResult CheckWebView2()
    {
        // Check for WebView2 via registry (user-level or machine-level installs)
        var regPaths = new[]
        {
            @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
            @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        };

        foreach (var path in regPaths)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                var version = key?.GetValue("pv") as string;
                if (!string.IsNullOrEmpty(version) && version != "0.0.0.0")
                    return CheckResult.Ok($"WebView2 {version}");
            }
            catch (System.Security.SecurityException)
            {
                // Registry access denied; continue checking other paths
            }
            catch (IOException)
            {
                // Registry I/O error; continue checking other paths
            }
        }

        return CheckResult.Fail("WebView2 runtime not found");
    }

    private static CheckResult CheckWebKitGtk()
    {
        var result = RunCommand("pkg-config", "--exists webkitgtk-6.0");
        if (result is not null)
            return CheckResult.Ok("libwebkitgtk-6.0");

        // Fallback: check ldconfig
        var ldconfig = RunCommand("ldconfig", "-p");
        if (ldconfig is not null && ldconfig.Contains("libwebkitgtk-6.0", StringComparison.Ordinal))
            return CheckResult.Ok("libwebkitgtk-6.0 (via ldconfig)");

        return CheckResult.Fail("libwebkitgtk-6.0 not found (install libwebkitgtk-6.0-dev)");
    }

    private static CheckResult CheckGitHubCli()
    {
        var version = RunCommand("gh", "--version");
        if (version is null)
            return CheckResult.Warn("not found (needed for native lib downloads)");

        var firstLine = version.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return CheckResult.Ok(firstLine);
    }

    private static CheckResult CheckCmake()
    {
        var version = RunCommand("cmake", "--version");
        if (version is null)
            return CheckResult.Warn("not found (needed for building native libs from source)");

        var firstLine = version.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return CheckResult.Ok(firstLine);
    }

    private static CheckResult CheckNinja()
    {
        var version = RunCommand("ninja", "--version");
        if (version is null)
            return CheckResult.Warn("not found (needed for building native libs from source)");

        return CheckResult.Ok(version.Trim());
    }

    private static CheckResult CheckSolutionBuild()
    {
        var slnx = FindSolutionFile();
        if (slnx is null)
            return CheckResult.Fail("cannot locate Ryn.slnx");

        Console.WriteLine();
        Console.WriteLine("  Running dotnet build (this may take a moment)...");

        var exitCode = RunCommandPassthrough("dotnet", $"build \"{slnx}\" --nologo -v quiet");
        if (exitCode == 0)
            return CheckResult.Ok("solution builds successfully");

        return CheckResult.Fail("solution build failed");
    }

    private static string? RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static int RunCommandPassthrough(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return 1;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            return 1;
        }
    }

    private static string? FindInteropDir()
    {
        // Walk up from current directory looking for src/Ryn.Interop
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "Ryn.Interop");
            if (Directory.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }

        return null;
    }

    private static string? FindSolutionFile()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "Ryn.slnx");
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }

        return null;
    }

    private enum CheckStatus { Ok, Warn, Fail }

    private readonly record struct CheckResult(CheckStatus Status, string? Detail)
    {
        internal static CheckResult Ok(string? detail = null) => new(CheckStatus.Ok, detail);
        internal static CheckResult Warn(string? detail = null) => new(CheckStatus.Warn, detail);
        internal static CheckResult Fail(string? detail = null) => new(CheckStatus.Fail, detail);
    }
}
