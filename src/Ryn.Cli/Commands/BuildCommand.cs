using System.Diagnostics;
using System.IO.Compression;

namespace Ryn.Cli.Commands;

internal static class BuildCommand
{
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
        var useAot = args.Contains("--aot");
        var embedContent = args.Contains("--embed");

        if (embedContent)
        {
            var wwwroot = Path.Combine(projectDir, "wwwroot");
            if (Directory.Exists(wwwroot))
            {
                var zipPath = Path.Combine(projectDir, "ryn_embedded_content.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(wwwroot, zipPath);
                Console.WriteLine("  Embedded wwwroot content into zip");
            }
            else
            {
                Console.Error.WriteLine("  --embed specified but no wwwroot/ directory found.");
                return 1;
            }
        }

        var dotnet = DotnetResolver.ResolveOrReport();
        if (dotnet is null)
            return 1;

        Console.WriteLine($"Building {projectName} for release...");

        var arguments = "publish -c Release --nologo";
        if (useAot)
        {
            arguments += " -p:PublishAot=true";
            Console.WriteLine("  NativeAOT enabled");
        }
        if (embedContent)
            arguments += " -p:RynEmbedContent=true";

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = dotnet,
            Arguments = arguments,
            WorkingDirectory = projectDir,
            UseShellExecute = false,
        });

        process?.WaitForExit();

        if (process?.ExitCode == 0)
        {
            var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            var ridPath = Path.Combine(projectDir, "bin", "Release", "net10.0", rid, "publish");
            var outputDir = Directory.Exists(ridPath)
                ? ridPath
                : Path.Combine(projectDir, "bin", "Release", "net10.0", "publish");
            Console.WriteLine();
            Console.WriteLine($"  Build succeeded: {outputDir}");
        }
        else
        {
            Console.Error.WriteLine("  Build failed.");
        }

        return process?.ExitCode ?? 1;
    }

    private static string? FindCsproj()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
        return files.Length == 1 ? files[0] : null;
    }
}
