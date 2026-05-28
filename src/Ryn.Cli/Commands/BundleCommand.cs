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

        var targetRid = GetArgValue(args, "--rid");
        if (targetRid is not null && !string.Equals(targetRid, RuntimeInformation.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"  Cross-RID bundling is not supported. Cannot bundle for '{targetRid}' on {RuntimeInformation.RuntimeIdentifier}.");
            Console.Error.WriteLine("  Build the bundle on the target platform, or use CI to produce platform-specific bundles.");
            return 1;
        }

        var publishArgs = BuildPublishArgs(args);

        Console.WriteLine($"Building {projectName} for release...");
        var buildProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = publishArgs,
            WorkingDirectory = projectDir,
            UseShellExecute = false,
        });
        buildProcess?.WaitForExit();

        if (buildProcess?.ExitCode != 0)
        {
            Console.Error.WriteLine("  Build failed.");
            return 1;
        }

        var publishDir = ResolvePublishDir(projectDir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return CreateMacAppBundle(projectDir, projectName, publishDir, args);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CreateWindowsBundle(projectDir, projectName, publishDir, args);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CreateLinuxBundle(projectDir, projectName, publishDir, args);

        Console.WriteLine();
        Console.WriteLine($"  Bundle ready: {publishDir}");
        return 0;
    }

    private static int CreateMacAppBundle(string projectDir, string projectName, string publishDir, ReadOnlySpan<string> args)
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

        // Read ryn.json + CLI args for bundle metadata
#pragma warning disable CA1308 // Bundle identifiers require lowercase by convention
        var bundleId = $"com.ryn.{projectName.ToLowerInvariant()}";
#pragma warning restore CA1308
        var bundleVersion = GetArgValue(args, "--version") ?? "1.0.0";
        var iconPath = GetArgValue(args, "--icon");
        var signIdentity = GetArgValue(args, "--sign");
        var notarize = args.Contains("--notarize");

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
                    if (bundle.TryGetProperty("version", out var ver))
                        bundleVersion = ver.GetString() ?? bundleVersion;
                    if (iconPath is null && bundle.TryGetProperty("icon", out var ico))
                        iconPath = ico.GetString();
                    if (signIdentity is null && bundle.TryGetProperty("sign", out var sig))
                        signIdentity = sig.GetString();
                }
            }
            catch (JsonException) { /* use defaults */ }
            catch (IOException) { /* use defaults */ }
        }

        // Write Info.plist
        var iconEntry = iconPath is not null
            ? $"\n            <key>CFBundleIconFile</key>\n            <string>{Path.GetFileName(iconPath)}</string>"
            : "";
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
                <string>{bundleVersion}</string>
                <key>CFBundleShortVersionString</key>
                <string>{bundleVersion}</string>
                <key>NSHighResolutionCapable</key>
                <true/>{iconEntry}
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

        // Copy icon if specified
        if (iconPath is not null && File.Exists(iconPath))
        {
            File.Copy(iconPath, Path.Combine(resourcesDir, Path.GetFileName(iconPath)), overwrite: true);
            Console.WriteLine($"  Icon: {Path.GetFileName(iconPath)}");
        }

        // Code sign if identity specified
        if (signIdentity is not null)
        {
            Console.WriteLine($"  Signing with identity: {signIdentity}");
            var signProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "codesign",
                Arguments = $"--force --deep --sign \"{signIdentity}\" \"{bundleDir}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
            });
            signProcess?.WaitForExit();
            if (signProcess?.ExitCode != 0)
                Console.Error.WriteLine("  Warning: code signing failed");
            else
                Console.WriteLine("  Code signing succeeded");
        }

        // Notarize if requested
        if (notarize && signIdentity is not null)
        {
            Console.WriteLine("  Submitting for notarization...");
            var zipPath = bundleDir + ".zip";
            Process.Start("ditto", $"-c -k --keepParent \"{bundleDir}\" \"{zipPath}\"")?.WaitForExit();
            var notarizeProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "xcrun",
                Arguments = $"notarytool submit \"{zipPath}\" --keychain-profile \"notarize\" --wait",
                UseShellExecute = false,
            });
            notarizeProcess?.WaitForExit();
            if (notarizeProcess?.ExitCode == 0)
            {
                Process.Start("xcrun", $"stapler staple \"{bundleDir}\"")?.WaitForExit();
                Console.WriteLine("  Notarization succeeded");
            }
            else
            {
                Console.Error.WriteLine("  Warning: notarization failed");
            }
            File.Delete(zipPath);
        }

        Console.WriteLine();
        Console.WriteLine($"  macOS app bundle created: {bundleDir}");
        Console.WriteLine($"  Run with: open \"{bundleDir}\"");
        return 0;
    }

    private static string? GetArgValue(ReadOnlySpan<string> args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return null;
    }

    private static int CreateWindowsBundle(string projectDir, string projectName, string publishDir, ReadOnlySpan<string> args)
    {
        var bundleDir = Path.Combine(projectDir, "bin", "bundle", projectName);

        if (Directory.Exists(bundleDir))
            Directory.Delete(bundleDir, recursive: true);

        Directory.CreateDirectory(bundleDir);

        // Read ryn.json + CLI args for bundle metadata
        var bundleVersion = GetArgValue(args, "--version") ?? "1.0.0";
        var iconPath = GetArgValue(args, "--icon");
        string? manufacturer = null;

        var rynJsonPath = Path.Combine(projectDir, "ryn.json");
        if (File.Exists(rynJsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(rynJsonPath));
                if (doc.RootElement.TryGetProperty("bundle", out var bundle))
                {
                    if (bundle.TryGetProperty("version", out var ver))
                        bundleVersion = ver.GetString() ?? bundleVersion;
                    if (iconPath is null && bundle.TryGetProperty("icon", out var ico))
                        iconPath = ico.GetString();
                    if (bundle.TryGetProperty("manufacturer", out var mfr))
                        manufacturer = mfr.GetString();
                }
            }
            catch (JsonException) { /* use defaults */ }
            catch (IOException) { /* use defaults */ }
        }

        manufacturer ??= projectName;

        // Copy published output
        foreach (var file in Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(publishDir, file);
            var destPath = Path.Combine(bundleDir, relativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null) Directory.CreateDirectory(destDir);
            File.Copy(file, destPath, overwrite: true);
        }

        // Copy wwwroot if present
        var wwwrootDir = Path.Combine(projectDir, "wwwroot");
        if (Directory.Exists(wwwrootDir))
        {
            var destWwwroot = Path.Combine(bundleDir, "wwwroot");
            foreach (var file in Directory.GetFiles(wwwrootDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(wwwrootDir, file);
                var destPath = Path.Combine(destWwwroot, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }
        }

        // Copy icon if specified
        if (iconPath is not null)
        {
            var resolvedIcon = Path.IsPathRooted(iconPath) ? iconPath : Path.Combine(projectDir, iconPath);
            if (File.Exists(resolvedIcon))
            {
                File.Copy(resolvedIcon, Path.Combine(bundleDir, Path.GetFileName(resolvedIcon)), overwrite: true);
                Console.WriteLine($"  Icon: {Path.GetFileName(resolvedIcon)}");
            }
        }

        // Create run.bat launcher
        var batContent = $"""
            @echo off
            cd /d "%~dp0"
            start "" "{projectName}.exe" %*
            """;
        File.WriteAllText(Path.Combine(bundleDir, "run.bat"), batContent);

        // Generate WiX v5 .wxs file for MSI creation
        var wxsPath = Path.Combine(bundleDir, $"{projectName}.wxs");
        GenerateWixFile(wxsPath, bundleDir, projectName, bundleVersion, manufacturer, iconPath);

        Console.WriteLine();
        Console.WriteLine($"  Windows bundle created: {bundleDir}");
        Console.WriteLine($"  Run with: {Path.Combine(bundleDir, "run.bat")}");
        Console.WriteLine();
        Console.WriteLine($"  To build MSI: install WiX v5 (`dotnet tool install --global wix`) then run `wix build {projectName}.wxs -o {projectName}.msi`");
        return 0;
    }

    private static void GenerateWixFile(string wxsPath, string bundleDir, string projectName, string bundleVersion, string manufacturer, string? iconPath)
    {
        var upgradeCode = GenerateDeterministicGuid(projectName, "upgrade");
        var componentGuid = GenerateDeterministicGuid(projectName, "component");
        var shortcutGuid = GenerateDeterministicGuid(projectName, "shortcut");

        // Collect all files from the bundle directory, excluding the .wxs itself
        var fileEntries = new System.Text.StringBuilder();
        var fileIndex = 0;
        foreach (var file in Directory.GetFiles(bundleDir, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".wxs", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(bundleDir, file);
            fileEntries.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"            <File Id=\"File{fileIndex}\" Source=\"{relativePath.Replace("/", "\\", StringComparison.Ordinal)}\" />");
            fileIndex++;
        }

        // Icon reference for Add/Remove Programs
        var iconElements = "";
        if (iconPath is not null)
        {
            var iconFileName = Path.GetFileName(iconPath);
            iconElements = $"""

                    <Icon Id="AppIcon" SourceFile="{iconFileName}" />
                    <Property Id="ARPPRODUCTICON" Value="AppIcon" />
            """;
        }

        var wxs = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
                <Package Name="{projectName}"
                         Version="{bundleVersion}"
                         Manufacturer="{manufacturer}"
                         UpgradeCode="{upgradeCode}"
                         Compressed="yes">

                    <MajorUpgrade DowngradeErrorMessage="A newer version of {projectName} is already installed." />
                    <MediaTemplate EmbedCab="yes" />{iconElements}

                    <StandardDirectory Id="ProgramFiles6432Folder">
                        <Directory Id="INSTALLFOLDER" Name="{projectName}">
                            <Component Id="MainComponent" Guid="{componentGuid}">
            {fileEntries}
                            </Component>
                        </Directory>
                    </StandardDirectory>

                    <StandardDirectory Id="ProgramMenuFolder">
                        <Component Id="StartMenuShortcut" Guid="{shortcutGuid}">
                            <Shortcut Id="AppShortcut"
                                      Name="{projectName}"
                                      Target="[INSTALLFOLDER]{projectName}.exe"
                                      WorkingDirectory="INSTALLFOLDER" />
                            <RegistryValue Root="HKCU"
                                           Key="Software\\{manufacturer}\\{projectName}"
                                           Name="StartMenuShortcut"
                                           Type="integer"
                                           Value="1"
                                           KeyPath="yes" />
                        </Component>
                    </StandardDirectory>

                    <Feature Id="Main" Level="1">
                        <ComponentRef Id="MainComponent" />
                        <ComponentRef Id="StartMenuShortcut" />
                    </Feature>
                </Package>
            </Wix>
            """;

        File.WriteAllText(wxsPath, wxs);
    }

    /// <summary>
    /// Generates a deterministic GUID from a seed string using a simple hash-based approach.
    /// This ensures the same project always gets the same GUIDs across builds.
    /// </summary>
    private static string GenerateDeterministicGuid(string projectName, string purpose)
    {
        var input = $"ryn:{projectName}:{purpose}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        // Use first 16 bytes of SHA256 to form a GUID
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes).ToString("D");
    }

    private static int CreateLinuxBundle(string projectDir, string projectName, string publishDir, ReadOnlySpan<string> args)
    {
        var bundleDir = Path.Combine(projectDir, "bin", "bundle", projectName + ".AppDir");

        if (Directory.Exists(bundleDir))
            Directory.Delete(bundleDir, recursive: true);

        var usrBin = Path.Combine(bundleDir, "usr", "bin");
        var usrShare = Path.Combine(bundleDir, "usr", "share", "applications");
        var usrShareIcons = Path.Combine(bundleDir, "usr", "share", "icons", "hicolor", "256x256", "apps");
        var usrLib = Path.Combine(bundleDir, "usr", "lib");

        Directory.CreateDirectory(usrBin);
        Directory.CreateDirectory(usrShare);
        Directory.CreateDirectory(usrShareIcons);
        Directory.CreateDirectory(usrLib);

        // Read ryn.json + CLI args for bundle metadata
        var bundleVersion = GetArgValue(args, "--version") ?? "1.0.0";
        var iconPath = GetArgValue(args, "--icon");
        var categories = "Utility;";
        string? comment = null;

        var rynJsonPath = Path.Combine(projectDir, "ryn.json");
        if (File.Exists(rynJsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(rynJsonPath));
                if (doc.RootElement.TryGetProperty("bundle", out var bundle))
                {
                    if (bundle.TryGetProperty("version", out var ver))
                        bundleVersion = ver.GetString() ?? bundleVersion;
                    if (iconPath is null && bundle.TryGetProperty("icon", out var ico))
                        iconPath = ico.GetString();
                    if (bundle.TryGetProperty("categories", out var cat))
                        categories = cat.GetString() ?? categories;
                    if (bundle.TryGetProperty("comment", out var cmt))
                        comment = cmt.GetString();
                }
            }
            catch (JsonException) { /* use defaults */ }
            catch (IOException) { /* use defaults */ }
        }

        // Copy published output
        foreach (var file in Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(publishDir, file);
            var destPath = Path.Combine(usrBin, relativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null) Directory.CreateDirectory(destDir);
            File.Copy(file, destPath, overwrite: true);
        }

        // Copy wwwroot if present
        var wwwrootDir = Path.Combine(projectDir, "wwwroot");
        if (Directory.Exists(wwwrootDir))
        {
            var destWwwroot = Path.Combine(usrBin, "wwwroot");
            foreach (var file in Directory.GetFiles(wwwrootDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(wwwrootDir, file);
                var destPath = Path.Combine(destWwwroot, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }
        }

        // Copy icon if specified
#pragma warning disable CA1308 // Desktop entry and icon filenames use lowercase by convention
        var lowerName = projectName.ToLowerInvariant();
#pragma warning restore CA1308
        var iconLine = "";
        if (iconPath is not null)
        {
            var resolvedIcon = Path.IsPathRooted(iconPath) ? iconPath : Path.Combine(projectDir, iconPath);
            if (File.Exists(resolvedIcon))
            {
                // Copy to AppDir root (required by AppImage spec)
                var rootIconDest = Path.Combine(bundleDir, $"{lowerName}{Path.GetExtension(resolvedIcon)}");
                File.Copy(resolvedIcon, rootIconDest, overwrite: true);

                // Also copy to hicolor icons for desktop integration
                File.Copy(resolvedIcon, Path.Combine(usrShareIcons, $"{lowerName}{Path.GetExtension(resolvedIcon)}"), overwrite: true);

                iconLine = $"\nIcon={lowerName}";
                Console.WriteLine($"  Icon: {Path.GetFileName(resolvedIcon)}");
            }
        }

        // Generate .desktop entry with categories and icon
        var commentLine = comment is not null ? $"\nComment={comment}" : "";
        var desktopEntry = $"""
            [Desktop Entry]
            Type=Application
            Name={projectName}
            Exec=usr/bin/{projectName}
            Categories={categories}{iconLine}{commentLine}
            Version={bundleVersion}
            Terminal=false
            """;
        File.WriteAllText(Path.Combine(bundleDir, $"{lowerName}.desktop"), desktopEntry);

        // Also place .desktop in usr/share/applications
        File.WriteAllText(Path.Combine(usrShare, $"{lowerName}.desktop"), desktopEntry);

        // Generate AppRun with proper environment setup
        var appRun = $$"""
            #!/bin/sh
            HERE="$(dirname "$(readlink -f "$0")")"
            export PATH="$HERE/usr/bin:$PATH"
            export LD_LIBRARY_PATH="$HERE/usr/lib:$LD_LIBRARY_PATH"
            export XDG_DATA_DIRS="$HERE/usr/share:$XDG_DATA_DIRS"
            export DOTNET_BUNDLE_EXTRACT_BASE_DIR="${DOTNET_BUNDLE_EXTRACT_BASE_DIR:-$HOME/.cache/{{lowerName}}}"
            exec "$HERE/usr/bin/{{projectName}}" "$@"
            """;
        var appRunPath = Path.Combine(bundleDir, "AppRun");
        File.WriteAllText(appRunPath, appRun);

        SetExecutable(appRunPath);

        var execPath = Path.Combine(usrBin, projectName);
        if (File.Exists(execPath))
            SetExecutable(execPath);

        // Generate build-appimage.sh helper script
        var buildScript = $"""
            #!/bin/sh
            set -e
            SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
            APPDIR="$SCRIPT_DIR/{projectName}.AppDir"
            OUTPUT="$SCRIPT_DIR/{projectName}-{bundleVersion}-x86_64.AppImage"

            if ! command -v appimagetool >/dev/null 2>&1; then
                echo "appimagetool not found in PATH. Downloading..."
                ARCH="$(uname -m)"
                TOOL_URL="https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$ARCH.AppImage"
                TOOL_PATH="$SCRIPT_DIR/appimagetool"
                if command -v curl >/dev/null 2>&1; then
                    curl -fSL "$TOOL_URL" -o "$TOOL_PATH"
                elif command -v wget >/dev/null 2>&1; then
                    wget -q "$TOOL_URL" -O "$TOOL_PATH"
                else
                    echo "Error: curl or wget required to download appimagetool"
                    exit 1
                fi
                chmod +x "$TOOL_PATH"
                APPIMAGETOOL="$TOOL_PATH"
            else
                APPIMAGETOOL="appimagetool"
            fi

            echo "Building AppImage..."
            ARCH="$(uname -m)" "$APPIMAGETOOL" "$APPDIR" "$OUTPUT"
            echo ""
            echo "AppImage created: $OUTPUT"
            """;
        var buildScriptPath = Path.Combine(projectDir, "bin", "bundle", "build-appimage.sh");
        File.WriteAllText(buildScriptPath, buildScript);
        SetExecutable(buildScriptPath);

        // Try to build AppImage directly if appimagetool is available
        var builtAppImage = false;
        var appImagePath = Path.Combine(projectDir, "bin", "bundle", $"{projectName}-{bundleVersion}-x86_64.AppImage");
        if (TryFindInPath("appimagetool"))
        {
            Console.WriteLine("  Found appimagetool, building AppImage...");
            var psi = new ProcessStartInfo
            {
                FileName = "appimagetool",
                UseShellExecute = false,
                WorkingDirectory = Path.Combine(projectDir, "bin", "bundle"),
            };
            psi.ArgumentList.Add(bundleDir);
            psi.ArgumentList.Add(appImagePath);
            psi.Environment["ARCH"] = "x86_64";

            var appImageProcess = Process.Start(psi);
            appImageProcess?.WaitForExit();
            builtAppImage = appImageProcess?.ExitCode == 0;
        }

        Console.WriteLine();
        Console.WriteLine($"  Linux AppDir created: {bundleDir}");
        if (builtAppImage)
        {
            Console.WriteLine($"  AppImage created: {appImagePath}");
        }
        else
        {
            Console.WriteLine($"  Run with: {appRunPath}");
            Console.WriteLine($"  To create AppImage: bash {buildScriptPath}");
        }
        return 0;
    }

    private static void SetExecutable(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "chmod",
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("+x");
        psi.ArgumentList.Add(path);
        Process.Start(psi)?.WaitForExit();
    }

    private static bool TryFindInPath(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "which",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add(command);

        try
        {
            var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string BuildPublishArgs(ReadOnlySpan<string> args)
    {
        var sb = new System.Text.StringBuilder("publish -c Release --nologo");

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--rid" && i + 1 < args.Length)
            {
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $" -r {args[i + 1]}");
                i++;
            }
            else if (args[i] == "--aot")
            {
                sb.Append(" -p:PublishAot=true");
            }
            else if (args[i] == "--self-contained")
            {
                sb.Append(" --self-contained");
            }
        }

        return sb.ToString();
    }

    private static string ResolvePublishDir(string projectDir)
    {
        var releaseDir = Path.Combine(projectDir, "bin", "Release", "net10.0");

        // Check RID-specific path first (NativeAOT publish uses this)
        var rid = RuntimeInformation.RuntimeIdentifier;
        var ridPath = Path.Combine(releaseDir, rid, "publish");
        if (Directory.Exists(ridPath))
            return ridPath;

        // Fall back to non-RID path
        return Path.Combine(releaseDir, "publish");
    }

    private static string? FindCsproj()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
        return files.Length == 1 ? files[0] : null;
    }
}
