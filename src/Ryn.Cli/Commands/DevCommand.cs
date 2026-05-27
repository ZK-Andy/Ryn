using System.Diagnostics;

namespace Ryn.Cli.Commands;

internal static class DevCommand
{
    private static Process? _appProcess;
    private static readonly object _lock = new();
    private static DateTime _lastChange = DateTime.MinValue;

    internal static int Execute(ReadOnlySpan<string> args)
    {
        var csproj = FindCsproj();
        if (csproj is null)
        {
            Console.Error.WriteLine("No .csproj file found in the current directory.");
            return 1;
        }

        var projectDir = Path.GetDirectoryName(csproj)!;
        var projectName = Path.GetFileNameWithoutExtension(csproj);

        Console.WriteLine($"Ryn dev mode — {projectName}");
        Console.WriteLine();

        // Initial build
        if (!Build(projectDir))
        {
            Console.Error.WriteLine("Initial build failed.");
            return 1;
        }

        // Launch app
        LaunchApp(projectDir, projectName);

        // Setup file watcher
        using var csWatcher = new FileSystemWatcher(projectDir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        csWatcher.Changed += (_, e) => OnSourceChanged(projectDir, projectName, e.FullPath);
        csWatcher.Created += (_, e) => OnSourceChanged(projectDir, projectName, e.FullPath);
        csWatcher.EnableRaisingEvents = true;

        // Handle Ctrl+C
        using var exitEvent = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutting down...");
            KillApp();
            exitEvent.Set();
        };

        Console.WriteLine("Watching for changes... (Ctrl+C to stop)");
        Console.WriteLine();

        exitEvent.Wait();
        return 0;
    }

    private static string? FindCsproj()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
        return files.Length == 1 ? files[0] : null;
    }

    private static bool Build(string projectDir)
    {
        Console.WriteLine("  Building...");
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --nologo -v q",
            WorkingDirectory = projectDir,
            UseShellExecute = false,
        });
        process?.WaitForExit();
        return process?.ExitCode == 0;
    }

    private static void LaunchApp(string projectDir, string projectName)
    {
        var outputDir = Path.Combine(projectDir, "bin", "Debug", "net10.0");
        var exeName = OperatingSystem.IsWindows() ? projectName + ".exe" : projectName;
        var executable = Path.Combine(outputDir, exeName);

        if (!File.Exists(executable))
        {
            Console.Error.WriteLine($"  Executable not found: {executable}");
            return;
        }

        Console.WriteLine($"  Launching {projectName}...");

        _appProcess = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = outputDir,
            UseShellExecute = false,
        });
    }

    private static void KillApp()
    {
        lock (_lock)
        {
            if (_appProcess is { HasExited: false })
            {
                try { _appProcess.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* already exited */ }
                _appProcess = null;
            }
        }
    }

    private static void OnSourceChanged(string projectDir, string projectName, string filePath)
    {
        // Skip obj/bin directories
        if (filePath.Contains(Path.Combine("", "obj", ""), StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains(Path.Combine("", "bin", ""), StringComparison.OrdinalIgnoreCase))
            return;

        // Debounce: ignore changes within 300ms of the last one
        var now = DateTime.UtcNow;
        if ((now - _lastChange).TotalMilliseconds < 300)
            return;
        _lastChange = now;

        Console.WriteLine($"  Change detected: {Path.GetFileName(filePath)}");

        lock (_lock)
        {
            KillApp();

            if (Build(projectDir))
            {
                LaunchApp(projectDir, projectName);
            }
            else
            {
                Console.Error.WriteLine("  Build failed. Waiting for next change...");
            }
        }
    }
}
