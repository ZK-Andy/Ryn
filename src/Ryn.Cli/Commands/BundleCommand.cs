using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Ryn.Cli.Commands;

internal static class BundleCommand
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

        // First build for release
        Console.WriteLine($"Building {projectName} for release...");
        var buildProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish -c Release --nologo",
            WorkingDirectory = projectDir,
            UseShellExecute = false,
        });
        buildProcess?.WaitForExit();

        if (buildProcess?.ExitCode != 0)
        {
            Console.Error.WriteLine("  Build failed.");
            return 1;
        }

        var publishDir = Path.Combine(projectDir, "bin", "Release", "net10.0", "publish");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return CreateMacAppBundle(projectDir, projectName, publishDir);

        // Other platforms: just report the publish directory
        Console.WriteLine();
        Console.WriteLine($"  Bundle ready: {publishDir}");
        Console.WriteLine("  (.app bundle creation is macOS only; other platforms produce a folder)");
        return 0;
    }

    private static int CreateMacAppBundle(string projectDir, string projectName, string publishDir)
    {
        var bundleDir = Path.Combine(projectDir, "bin", "bundle", $"{projectName}.app");
        var contentsDir = Path.Combine(bundleDir, "Contents");
        var macosDir = Path.Combine(contentsDir, "MacOS");
        var resourcesDir = Path.Combine(contentsDir, "Resources");

        // Clean previous bundle
        if (Directory.Exists(bundleDir))
            Directory.Delete(bundleDir, recursive: true);

        Directory.CreateDirectory(macosDir);
        Directory.CreateDirectory(resourcesDir);

        // Read ryn.json for bundle metadata
#pragma warning disable CA1308 // Bundle identifiers require lowercase by convention
        var bundleId = $"com.ryn.{projectName.ToLowerInvariant()}";
#pragma warning restore CA1308
        var rynJsonPath = Path.Combine(projectDir, "ryn.json");
        if (File.Exists(rynJsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(rynJsonPath));
                if (doc.RootElement.TryGetProperty("bundle", out var bundle))
                {
                    if (bundle.TryGetProperty("identifier", out var id))
                        bundleId = id.GetString() ?? bundleId;
                }
            }
            catch (JsonException) { /* use defaults */ }
            catch (IOException) { /* use defaults */ }
        }

        // Write Info.plist
        var plist = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>CFBundleExecutable</key>
                <string>{projectName}</string>
                <key>CFBundleIdentifier</key>
                <string>{bundleId}</string>
                <key>CFBundleName</key>
                <string>{projectName}</string>
                <key>CFBundlePackageType</key>
                <string>APPL</string>
                <key>CFBundleVersion</key>
                <string>1.0.0</string>
                <key>CFBundleShortVersionString</key>
                <string>1.0.0</string>
                <key>NSHighResolutionCapable</key>
                <true/>
            </dict>
            </plist>
            """;

        File.WriteAllText(Path.Combine(contentsDir, "Info.plist"), plist);

        // Copy published output to MacOS/
        foreach (var file in Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(publishDir, file);
            var destPath = Path.Combine(macosDir, relativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null) Directory.CreateDirectory(destDir);
            File.Copy(file, destPath, overwrite: true);
        }

        // Make the executable... executable
        var execPath = Path.Combine(macosDir, projectName);
        if (File.Exists(execPath))
        {
            Process.Start("chmod", $"+x \"{execPath}\"")?.WaitForExit();
        }

        // Copy wwwroot to Resources/ if it exists
        var wwwrootDir = Path.Combine(projectDir, "wwwroot");
        if (Directory.Exists(wwwrootDir))
        {
            foreach (var file in Directory.GetFiles(wwwrootDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(wwwrootDir, file);
                var destPath = Path.Combine(resourcesDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  macOS app bundle created: {bundleDir}");
        Console.WriteLine($"  Run with: open \"{bundleDir}\"");
        return 0;
    }

    private static string? FindCsproj()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
        return files.Length == 1 ? files[0] : null;
    }
}
