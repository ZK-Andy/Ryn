using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ryn.Plugins.Updater;

public sealed class UpdaterService : IDisposable
{
    private readonly UpdaterOptions _options;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    internal UpdaterService(UpdaterOptions options)
    {
        _options = options;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Ryn-Updater/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    public async ValueTask<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.GitHubOwner) || string.IsNullOrEmpty(_options.GitHubRepo))
            throw new InvalidOperationException("GitHubOwner and GitHubRepo must be configured.");

        var url = $"https://api.github.com/repos/{_options.GitHubOwner}/{_options.GitHubRepo}/releases/latest";

        var requestUri = new Uri(url);
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var release = JsonSerializer.Deserialize(json, UpdaterJsonContext.Default.GitHubRelease);

        if (release is null)
            return null;

        var remoteVersion = ParseVersion(release.TagName);
        if (remoteVersion is null)
            return null;

        var currentVersion = _options.CurrentVersion is not null
            ? ParseVersion(_options.CurrentVersion)
            : null;

        if (currentVersion is not null && remoteVersion <= currentVersion)
            return null;

        var assetUrl = FindMatchingAsset(release);
        if (assetUrl is null)
            return null;

        return new UpdateInfo
        {
            Version = release.TagName,
            ReleaseUrl = new Uri(release.HtmlUrl ?? $"https://github.com/{_options.GitHubOwner}/{_options.GitHubRepo}/releases"),
            AssetUrl = new Uri(assetUrl),
            ReleaseNotes = release.Body,
        };
    }

    public async ValueTask<string> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        using var request = new HttpRequestMessage(HttpMethod.Get, update.AssetUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var extension = GetAssetExtension(update.AssetUrl.AbsoluteUri);
        var tempPath = Path.Combine(Path.GetTempPath(), $"ryn_update_{Guid.NewGuid():N}{extension}");

        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var contentStreamDispose = contentStream.ConfigureAwait(false);
        var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await using var fileStreamDispose = fileStream.ConfigureAwait(false);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalBytes > 0)
                progress?.Report((double)totalRead / totalBytes);
        }

        progress?.Report(1.0);
        return tempPath;
    }

    public ValueTask ApplyUpdateAsync(string downloadPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(downloadPath);

        if (!File.Exists(downloadPath))
            throw new FileNotFoundException("Downloaded update file not found.", downloadPath);

        var currentExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine current executable path.");

        if (OperatingSystem.IsMacOS())
            ApplyMacOS(downloadPath, currentExePath);
        else if (OperatingSystem.IsWindows())
            ApplyWindows(downloadPath, currentExePath);
        else if (OperatingSystem.IsLinux())
            ApplyLinux(downloadPath, currentExePath);
        else
            throw new PlatformNotSupportedException("Auto-update is not supported on this platform.");

        return ValueTask.CompletedTask;
    }

    private static void ApplyMacOS(string downloadPath, string currentExePath)
    {
        // Determine the .app bundle root — walk up from the executable
        // e.g. /Applications/MyApp.app/Contents/MacOS/MyApp -> /Applications/MyApp.app
        var appBundle = FindAppBundle(currentExePath);

        if (appBundle is not null)
        {
            // Replace the entire .app bundle
            var parentDir = Path.GetDirectoryName(appBundle)!;
            var appName = Path.GetFileName(appBundle);
            var backupPath = Path.Combine(parentDir, $".{appName}.bak");

            var script = string.Join(" && ",
                $"mv \"{appBundle}\" \"{backupPath}\"",
                downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    ? $"ditto -xk \"{downloadPath}\" \"{parentDir}\""
                    : $"cp -R \"{downloadPath}\" \"{appBundle}\"",
                $"open -n \"{Path.Combine(parentDir, appName)}\"",
                $"rm -rf \"{backupPath}\"");

            LaunchDetachedScript("/bin/sh", $"-c \"{EscapeShellArg(script)}\"");
        }
        else
        {
            // Standalone binary — replace in place
            ReplaceStandaloneBinary(downloadPath, currentExePath);
        }

        Environment.Exit(0);
    }

    private static void ApplyWindows(string downloadPath, string currentExePath)
    {
        var currentDir = Path.GetDirectoryName(currentExePath)!;
        var currentExeName = Path.GetFileName(currentExePath);
        var backupPath = $"{currentExePath}.bak";

        // Batch script that waits for the process to exit, replaces the exe, restarts
        var batchScript = $"""
            @echo off
            timeout /t 2 /nobreak >nul
            move /y "{currentExePath}" "{backupPath}"
            move /y "{downloadPath}" "{currentExePath}"
            start "" "{currentExePath}"
            del "{backupPath}"
            del "%~f0"
            """;

        var batchPath = Path.Combine(Path.GetTempPath(), $"ryn_update_{Guid.NewGuid():N}.bat");
        File.WriteAllText(batchPath, batchScript);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batchPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        Environment.Exit(0);
    }

    private static void ApplyLinux(string downloadPath, string currentExePath)
    {
        ReplaceStandaloneBinary(downloadPath, currentExePath);
        Environment.Exit(0);
    }

    private static void ReplaceStandaloneBinary(string downloadPath, string currentExePath)
    {
        var backupPath = $"{currentExePath}.bak";

        var script = string.Join(" && ",
            $"mv \"{currentExePath}\" \"{backupPath}\"",
            $"mv \"{downloadPath}\" \"{currentExePath}\"",
            $"chmod +x \"{currentExePath}\"",
            $"\"{currentExePath}\" &",
            $"rm -f \"{backupPath}\"");

        LaunchDetachedScript("/bin/sh", $"-c \"{EscapeShellArg(script)}\"");
    }

    private static string? FindAppBundle(string executablePath)
    {
        // Walk up the directory tree looking for a .app directory
        var dir = Path.GetDirectoryName(executablePath);
        while (dir is not null)
        {
            if (dir.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static void LaunchDetachedScript(string shell, string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = shell,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
    }

    private static string EscapeShellArg(string arg)
    {
        return arg.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private string? FindMatchingAsset(GitHubRelease release)
    {
        if (release.Assets is null || release.Assets.Length == 0)
            return null;

        if (!string.IsNullOrEmpty(_options.AssetPattern))
        {
            var regex = new Regex(_options.AssetPattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
            foreach (var asset in release.Assets)
            {
                if (asset.Name is not null && regex.IsMatch(asset.Name))
                    return asset.BrowserDownloadUrl;
            }
            return null;
        }

        // Auto-detect platform asset if no pattern specified
        var platformHints = GetPlatformHints();
        foreach (var asset in release.Assets)
        {
            if (asset.Name is null)
                continue;

            foreach (var hint in platformHints)
            {
                if (asset.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return asset.BrowserDownloadUrl;
            }
        }

        return null;
    }

    private static string[] GetPlatformHints()
    {
        if (OperatingSystem.IsMacOS())
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                ? ["macos-arm64", "osx-arm64", "darwin-arm64", "macos", "osx", "darwin"]
                : ["macos-x64", "osx-x64", "darwin-x64", "macos", "osx", "darwin"];

        if (OperatingSystem.IsWindows())
            return ["win-x64", "windows-x64", "win64", "windows"];

        if (OperatingSystem.IsLinux())
            return ["linux-x64", "linux-amd64", "linux"];

        return [];
    }

    private static Version? ParseVersion(string? versionString)
    {
        if (string.IsNullOrEmpty(versionString))
            return null;

        // Strip leading 'v' prefix (e.g. "v1.2.3" -> "1.2.3")
        var normalized = versionString.StartsWith('v') || versionString.StartsWith('V')
            ? versionString[1..]
            : versionString;

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private static string GetAssetExtension(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var ext = Path.GetExtension(path);
        return string.IsNullOrEmpty(ext) ? ".bin" : ext;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
