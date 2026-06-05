using System.Diagnostics;

namespace Ryn.Cli.Commands;

internal static class DevCommand
{
    private static Process? _appProcess;
    private static readonly object _lock = new();
    private static DateTime _lastCsChange = DateTime.MinValue;
    private static DateTime _lastFrontendChange = DateTime.MinValue;
    private static string _dotnet = "dotnet";

    internal static int Execute(ReadOnlySpan<string> args)
    {
        var csproj = FindCsproj();
        if (csproj is null)
        {
            Console.Error.WriteLine("No .csproj file found in the current directory.");
            return 1;
        }

        var dotnet = DotnetResolver.ResolveOrReport();
        if (dotnet is null)
            return 1;
        _dotnet = dotnet;

        var projectDir = Path.GetDirectoryName(csproj)!;
        var projectName = Path.GetFileNameWithoutExtension(csproj);
        var wwwrootDir = Path.Combine(projectDir, "wwwroot");

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

        // Setup C# file watcher
        using var csWatcher = new FileSystemWatcher(projectDir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        csWatcher.Changed += (_, e) => OnSourceChanged(projectDir, projectName, e.FullPath);
        csWatcher.Created += (_, e) => OnSourceChanged(projectDir, projectName, e.FullPath);
        csWatcher.EnableRaisingEvents = true;

        // Setup frontend (wwwroot) file watcher
        FileSystemWatcher? frontendWatcher = null;
        if (Directory.Exists(wwwrootDir))
        {
            frontendWatcher = new FileSystemWatcher(wwwrootDir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            };
            frontendWatcher.Changed += (_, e) => OnFrontendChanged(projectDir, projectName, e.FullPath);
            frontendWatcher.Created += (_, e) => OnFrontendChanged(projectDir, projectName, e.FullPath);
            frontendWatcher.Deleted += (_, e) => OnFrontendChanged(projectDir, projectName, e.FullPath);
            frontendWatcher.Renamed += (_, e) => OnFrontendChanged(projectDir, projectName, e.FullPath);
            frontendWatcher.EnableRaisingEvents = true;
            Console.WriteLine("Watching wwwroot/ for frontend changes (no rebuild)");
        }

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
        frontendWatcher?.Dispose();
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
        var psi = new ProcessStartInfo
        {
            FileName = _dotnet,
            WorkingDirectory = projectDir,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add("--nologo");
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("q");

        var process = Process.Start(psi);
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

    private static void SyncFrontendFiles(string projectDir)
    {
        var sourceDir = Path.Combine(projectDir, "wwwroot");
        var targetDir = Path.Combine(projectDir, "bin", "Debug", "net10.0", "wwwroot");

        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(targetDir);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = sourceFile[sourceDir.Length..].TrimStart(Path.DirectorySeparatorChar);
            var targetFile = Path.Combine(targetDir, relativePath);

            var targetFileDir = Path.GetDirectoryName(targetFile);
            if (targetFileDir is not null)
                Directory.CreateDirectory(targetFileDir);

            File.Copy(sourceFile, targetFile, overwrite: true);
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
        if ((now - _lastCsChange).TotalMilliseconds < 300)
            return;
        _lastCsChange = now;

        Console.WriteLine($"  C# change detected: {Path.GetFileName(filePath)}");
        Console.WriteLine("  Rebuilding...");

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

    private static void OnFrontendChanged(string projectDir, string projectName, string filePath)
    {
        // Debounce: ignore changes within 300ms of the last one
        var now = DateTime.UtcNow;
        if ((now - _lastFrontendChange).TotalMilliseconds < 300)
            return;
        _lastFrontendChange = now;

        Console.WriteLine($"  Frontend change detected: {Path.GetFileName(filePath)}");
        Console.WriteLine("  Refreshing (no rebuild)...");

        lock (_lock)
        {
            KillApp();
            SyncFrontendFiles(projectDir);
            LaunchApp(projectDir, projectName);
        }
    }
}
