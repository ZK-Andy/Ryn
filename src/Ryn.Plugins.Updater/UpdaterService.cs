using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ryn.Plugins.Updater;

public sealed class UpdaterService : IDisposable
{
    private readonly UpdaterOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, VerifiedDownload> _verifiedDownloads = new(StringComparer.Ordinal);
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

        // Downgrade protection: the effective current version is the max of the configured/assembly
        // version and the persisted monotonic floor. An unknown current version is NOT "accept anything".
        var effectiveCurrent = GetEffectiveCurrentVersion();
        if (effectiveCurrent is not null && remoteVersion <= effectiveCurrent)
            return null;

        var asset = FindMatchingAsset(release);
        if (asset?.BrowserDownloadUrl is null)
            return null;

        var assetUri = ValidateDownloadUri(asset.BrowserDownloadUrl);

        // Locate the detached signature asset (assetName + suffix).
        var sigName = asset.Name + _options.SignatureAssetSuffix;
        var sigAsset = Array.Find(release.Assets ?? [], a =>
            string.Equals(a.Name, sigName, StringComparison.OrdinalIgnoreCase));
        var sigUri = sigAsset?.BrowserDownloadUrl is { } su ? ValidateDownloadUri(su) : null;

        return new UpdateInfo
        {
            Version = release.TagName,
            ReleaseUrl = new Uri(release.HtmlUrl ?? $"https://github.com/{_options.GitHubOwner}/{_options.GitHubRepo}/releases"),
            AssetUrl = assetUri,
            SignatureUrl = sigUri,
            ReleaseNotes = release.Body,
        };
    }

    /// <summary>
    /// Downloads the update, verifies its detached signature against the embedded public key, and — only
    /// if verification succeeds — registers it and returns an opaque handle. The raw file path is never
    /// exposed to or accepted from the frontend; <see cref="ApplyUpdateAsync"/> takes this handle.
    /// </summary>
    public async ValueTask<string> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (string.IsNullOrEmpty(_options.PublicKey))
            throw new InvalidOperationException(
                "Updater.PublicKey is not configured. Refusing to download an update that cannot be cryptographically verified.");

        if (update.SignatureUrl is null)
            throw new SecurityException(
                $"No detached signature asset ('{_options.SignatureAssetSuffix}') was found for this release. Refusing to proceed.");

        ValidateDownloadUri(update.AssetUrl.AbsoluteUri);
        ValidateDownloadUri(update.SignatureUrl.AbsoluteUri);

        var extension = GetAssetExtension(update.AssetUrl.AbsoluteUri);
        var tempPath = Path.Combine(Path.GetTempPath(), $"ryn_update_{Guid.NewGuid():N}{extension}");

        try
        {
            await DownloadToFileAsync(update.AssetUrl, tempPath, progress, cancellationToken).ConfigureAwait(false);

            // Fetch + verify the detached signature BEFORE the file is ever eligible to run.
            // The signature is tiny; cap the read so a hostile endpoint can't stream gigabytes.
            using var sigResponse = await _httpClient.GetAsync(update.SignatureUrl, cancellationToken).ConfigureAwait(false);
            sigResponse.EnsureSuccessStatusCode();
            if (sigResponse.Content.Headers.ContentLength is > 8192)
                throw new SecurityException("Update signature asset is implausibly large.");
            var sigBytes = await sigResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (sigBytes.Length > 8192)
                throw new SecurityException("Update signature asset is implausibly large.");
            var signatureBase64 = System.Text.Encoding.UTF8.GetString(sigBytes).Trim();
            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(signatureBase64);
            }
            catch (FormatException ex)
            {
                throw new SecurityException("Update signature asset is not valid base64.", ex);
            }

            var data = await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
            if (!UpdateSignature.Verify(data, signature, _options.PublicKey))
                throw new SecurityException("Update signature verification failed. The download is corrupt or untrusted.");

            var handle = Guid.NewGuid().ToString("N");
            _verifiedDownloads[handle] = new VerifiedDownload(tempPath, update.Version);
            return handle;
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private async Task DownloadToFileAsync(Uri uri, string tempPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        if (_options.MaxDownloadBytes > 0 && totalBytes > _options.MaxDownloadBytes)
            throw new SecurityException($"Update asset ({totalBytes} bytes) exceeds the maximum allowed size ({_options.MaxDownloadBytes} bytes).");

        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var contentStreamDispose = contentStream.ConfigureAwait(false);
        var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await using var fileStreamDispose = fileStream.ConfigureAwait(false);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += bytesRead;
            if (_options.MaxDownloadBytes > 0 && totalRead > _options.MaxDownloadBytes)
                throw new SecurityException($"Update asset exceeds the maximum allowed size ({_options.MaxDownloadBytes} bytes).");

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

            if (totalBytes > 0)
                progress?.Report((double)totalRead / totalBytes);
        }

        progress?.Report(1.0);
    }

    /// <summary>
    /// Applies a previously downloaded-and-verified update identified by the opaque handle returned from
    /// <see cref="DownloadUpdateAsync"/>. Rejects any handle not produced this session — the frontend can
    /// never point this at an arbitrary file.
    /// </summary>
    public ValueTask ApplyUpdateAsync(string downloadHandle, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(downloadHandle);

        if (!_verifiedDownloads.TryGetValue(downloadHandle, out var verified))
            throw new SecurityException("Unknown or unverified update handle. Download the update through the updater before applying.");

        if (!File.Exists(verified.Path))
            throw new FileNotFoundException("The verified update file is no longer present.", verified.Path);

        var currentExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine current executable path.");

        // Record the new version as the monotonic downgrade floor before relaunching.
        PersistVersionFloor(verified.Version);

        if (OperatingSystem.IsMacOS())
            ApplyMacOS(verified.Path, currentExePath);
        else if (OperatingSystem.IsWindows())
            ApplyWindows(verified.Path, currentExePath);
        else if (OperatingSystem.IsLinux())
            ApplyLinux(verified.Path, currentExePath);
        else
            throw new PlatformNotSupportedException("Auto-update is not supported on this platform.");

        return ValueTask.CompletedTask;
    }

    // ---- platform apply (no shell-string interpolation; all paths passed as positional argv) ----

    private static void ApplyMacOS(string downloadPath, string currentExePath)
    {
        var appBundle = FindAppBundle(currentExePath);

        if (appBundle is not null)
        {
            var parentDir = Path.GetDirectoryName(appBundle)!;
            var appName = Path.GetFileName(appBundle);
            var backupPath = Path.Combine(parentDir, $".{appName}.bak");

            string newAppSource;
            if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Extract in managed code, then locate the .app inside the staging directory.
                var staging = Path.Combine(Path.GetTempPath(), $"ryn_update_stage_{Guid.NewGuid():N}");
                System.IO.Compression.ZipFile.ExtractToDirectory(downloadPath, staging);
                newAppSource = Directory.EnumerateDirectories(staging, "*.app", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new InvalidOperationException("No .app bundle found inside the downloaded archive.");
            }
            else
            {
                newAppSource = downloadPath;
            }

            // Constant script body; paths arrive as $1..$4 and are never interpolated into the script text.
            const string script =
                "app=\"$1\"; backup=\"$2\"; src=\"$3\"; relaunch=\"$4\"; " +
                "rm -rf \"$backup\"; mv \"$app\" \"$backup\" && cp -R \"$src\" \"$app\" && open -n \"$relaunch\" && rm -rf \"$backup\"";
            LaunchDetachedShell(script, appBundle, backupPath, newAppSource, appBundle);
        }
        else
        {
            ReplaceStandaloneBinary(downloadPath, currentExePath);
        }

        Environment.Exit(0);
    }

    private static void ApplyWindows(string downloadPath, string currentExePath)
    {
        var backupPath = $"{currentExePath}.bak";

        // Constant batch body using positional parameters (%1=current, %2=download, %3=backup).
        const string batchScript =
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            "move /y %1 %3\r\n" +
            "move /y %2 %1\r\n" +
            "start \"\" %1\r\n" +
            "del %3\r\n" +
            "del \"%~f0\"\r\n";

        var batchPath = Path.Combine(Path.GetTempPath(), $"ryn_update_{Guid.NewGuid():N}.bat");
        File.WriteAllText(batchPath, batchScript);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(batchPath);
        psi.ArgumentList.Add(currentExePath);
        psi.ArgumentList.Add(downloadPath);
        psi.ArgumentList.Add(backupPath);
        Process.Start(psi);

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
        const string script =
            "cur=\"$1\"; dl=\"$2\"; backup=\"$3\"; " +
            "mv \"$cur\" \"$backup\" && mv \"$dl\" \"$cur\" && chmod +x \"$cur\" && \"$cur\" & rm -f \"$backup\"";
        LaunchDetachedShell(script, currentExePath, downloadPath, backupPath);
    }

    private static string? FindAppBundle(string executablePath)
    {
        var dir = Path.GetDirectoryName(executablePath);
        while (dir is not null)
        {
            if (dir.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>Launches <c>/bin/sh -c &lt;constant-script&gt; sh &lt;positional-args...&gt;</c> — injection-proof because the script is constant and every path is a separate argv element.</summary>
    private static void LaunchDetachedShell(string script, params string[] positionalArgs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("sh"); // $0
        foreach (var arg in positionalArgs)
            psi.ArgumentList.Add(arg);
        Process.Start(psi);
    }

    // ---- version / floor ----

    private Version? GetEffectiveCurrentVersion()
    {
        var configured = _options.CurrentVersion is not null ? ParseVersion(_options.CurrentVersion) : null;
        configured ??= ParseVersion(Assembly.GetEntryAssembly()?.GetName().Version?.ToString());
        var floor = ReadVersionFloor();

        if (configured is null) return floor;
        if (floor is null) return configured;
        return configured >= floor ? configured : floor;
    }

    private string VersionFloorFile() =>
        _options.VersionFloorPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Ryn", "updates", $"{_options.GitHubOwner}_{_options.GitHubRepo}.floor");

    private Version? ReadVersionFloor()
    {
        try
        {
            var path = VersionFloorFile();
            return File.Exists(path) ? ParseVersion(File.ReadAllText(path).Trim()) : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void PersistVersionFloor(string version)
    {
        try
        {
            var parsed = ParseVersion(version);
            if (parsed is null) return;
            var existing = ReadVersionFloor();
            if (existing is not null && parsed <= existing) return;

            var path = VersionFloorFile();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, parsed.ToString());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ---- helpers ----

    private Uri ValidateDownloadUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new SecurityException($"Update download URL '{url}' is not a valid absolute URL.");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException($"Update download URL must use HTTPS (got '{uri.Scheme}').");

        var host = uri.Host;
        var ok = false;
        foreach (var allowed in _options.AllowedDownloadHosts)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
            {
                ok = true;
                break;
            }
        }

        if (!ok)
            throw new SecurityException($"Update download host '{host}' is not in the allowed list.");

        return uri;
    }

    private GitHubAsset? FindMatchingAsset(GitHubRelease release)
    {
        if (release.Assets is null || release.Assets.Length == 0)
            return null;

        bool IsSignature(GitHubAsset a) =>
            a.Name is not null && a.Name.EndsWith(_options.SignatureAssetSuffix, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(_options.AssetPattern))
        {
            var regex = new Regex(_options.AssetPattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
            foreach (var asset in release.Assets)
            {
                if (!IsSignature(asset) && asset.Name is not null && regex.IsMatch(asset.Name))
                    return asset;
            }
            return null;
        }

        var platformHints = GetPlatformHints();
        foreach (var asset in release.Assets)
        {
            if (asset.Name is null || IsSignature(asset))
                continue;

            foreach (var hint in platformHints)
            {
                if (asset.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return asset;
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

        var normalized = versionString.StartsWith('v') || versionString.StartsWith('V')
            ? versionString[1..]
            : versionString;

        // Drop SemVer prerelease / build metadata so e.g. "1.2.3-rc1+build" parses as 1.2.3.
        var dash = normalized.IndexOfAny(['-', '+']);
        if (dash >= 0)
            normalized = normalized[..dash];

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private static string GetAssetExtension(string url)
    {
        var uri = new Uri(url);
        var ext = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrEmpty(ext) ? ".bin" : ext;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _httpClient.Dispose();
    }

    private sealed record VerifiedDownload(string Path, string Version);
}
