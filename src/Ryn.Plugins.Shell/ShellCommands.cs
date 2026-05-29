using System.Diagnostics;
using System.Text.Json;
using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

public static class ShellCommands
{
    private static ShellOptions? _options;

    internal static ShellOptions? Options => _options;

    private static readonly string[] DefaultOpenSchemes = ["http", "https", "mailto"];

    private static readonly StringComparer CommandNameComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    internal static void Configure(ShellOptions options)
    {
        _options = options;
        // Resolve both legacy allow-list names AND the names of per-argument command scopes, so a
        // scoped command is launchable but still argument-checked.
        var names = new List<string>(options.AllowedCommands);
        foreach (var scope in options.CommandScopes)
            names.Add(scope.Name);
        (_resolvedBareCommands, _allowedFullPaths) = ResolveAllowlist(names);
    }

    [RynCommand("shell.execute")]
    public static string Execute(string command, string argsJson)
    {
        var args = ParseArgs(argsJson);
        var resolvedCommand = ValidateInvocation(command, args);

        var psi = new ProcessStartInfo
        {
            FileName = resolvedCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        ApplyProcessPolicy(psi);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {command}");

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = stderrTask.GetAwaiter().GetResult();
        process.WaitForExit();

        var output = new ProcessOutput(stdout, stderr, process.ExitCode);
        return JsonSerializer.Serialize(output, ShellJsonContext.Default.ProcessOutput);
    }

    [RynCommand("shell.open")]
    public static void Open(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        ValidateOpenTarget(url);

        if (OperatingSystem.IsMacOS())
            Process.Start(new ProcessStartInfo { FileName = "open", ArgumentList = { url }, UseShellExecute = false });
        else if (OperatingSystem.IsLinux())
            Process.Start(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { url }, UseShellExecute = false });
        else if (OperatingSystem.IsWindows())
            // UseShellExecute is required to launch the default handler, but only after we have
            // verified the target is a permitted URL scheme (never a bare path or executable).
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    /// <summary>
    /// Validates that <paramref name="url"/> is an absolute URL whose scheme is in the allowlist.
    /// Rejects bare paths, <c>file://</c>, and any scheme not explicitly permitted — this is what stops
    /// <c>shell.open</c> from launching arbitrary executables, <c>.app</c> bundles, or local files.
    /// </summary>
    internal static void ValidateOpenTarget(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new UnauthorizedAccessException($"shell.open target '{url}' is not an absolute URL");

        var allowed = _options?.AllowedOpenSchemes is { Count: > 0 } configured
            ? configured
            : (IReadOnlyList<string>)DefaultOpenSchemes;

        var ok = false;
        foreach (var scheme in allowed)
        {
            if (string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
            {
                ok = true;
                break;
            }
        }

        if (!ok)
            throw new UnauthorizedAccessException(
                $"shell.open scheme '{uri.Scheme}' is not permitted (allowed: {string.Join(", ", allowed)})");
    }

    /// <summary>
    /// The single choke point through which <c>execute</c>, <c>spawn</c>, and <c>pty</c> all pass.
    /// Resolves and authorizes the binary, then enforces the argument policy. Returns the resolved path.
    /// </summary>
    internal static string ValidateInvocation(string command, string[] args)
    {
        var resolved = ValidateAndResolveCommand(command);
        EnforceArgumentPolicy(command, args);
        return resolved;
    }

    internal static string ValidateAndResolveCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Fail closed: an unconfigured shell never runs anything. (Previously returned the command
        // unchecked, which combined with a swallowed plugin-init failure could disable the allowlist.)
        if (_options is null)
            throw new UnauthorizedAccessException("Shell execution is not configured (allowlist unavailable)");

        if (_resolvedBareCommands is null || _allowedFullPaths is null)
            throw new UnauthorizedAccessException("Shell execution is not configured (allowlist unavailable)");

        if (_resolvedBareCommands.Count == 0 && _allowedFullPaths.Count == 0)
            throw new UnauthorizedAccessException("Shell execution is disabled (no commands in allowlist)");

        var hasPathSeparator = command.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || command.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

        if (hasPathSeparator)
        {
            var canonical = Path.GetFullPath(command);
            if (_allowedFullPaths.Contains(canonical))
                return canonical;
            throw new UnauthorizedAccessException($"Command path '{command}' is not in the allowed list");
        }

        if (_resolvedBareCommands.TryGetValue(command, out var resolvedPath))
            return resolvedPath;

        throw new UnauthorizedAccessException($"Command '{command}' is not in the allowed list");
    }

    private static Dictionary<string, string>? _resolvedBareCommands;
    private static HashSet<string>? _allowedFullPaths;

    private static (Dictionary<string, string> bare, HashSet<string> full) ResolveAllowlist(List<string> commands)
    {
        var bare = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var full = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var dirs = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var cmd in commands)
        {
            var hasPath = cmd.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || cmd.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

            if (hasPath)
            {
                full.Add(Path.GetFullPath(cmd));
                continue;
            }

            var resolved = FindInPath(cmd, dirs);
            if (resolved is not null)
                bare[cmd] = resolved;
            // If bare command can't be found in PATH at config time, it is
            // silently excluded — fail closed. The command will be rejected
            // at invocation time since it won't be in either map.
        }
        return (bare, full);
    }

    private static string? FindInPath(string command, string[] dirs)
    {
        foreach (var dir in dirs)
        {
            var candidate = Path.Combine(dir, command);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            if (OperatingSystem.IsWindows())
            {
                foreach (var ext in new[] { ".exe", ".cmd", ".bat" })
                {
                    var withExt = candidate + ext;
                    if (File.Exists(withExt))
                        return Path.GetFullPath(withExt);
                }
            }
        }
        return null;
    }

    private static readonly string[] DangerousPrefixes =
        ["--proxy", "--socks", "--upload-file", "--data-binary"];

    /// <summary>
    /// Enforces, in order: the built-in argument denylist, any integrator deny-prefixes, and — when the
    /// command has one or more <see cref="CommandScope"/>s — that the argv matches a scope exactly.
    /// </summary>
    internal static void EnforceArgumentPolicy(string command, string[] args)
    {
        ValidateArguments(args);

        var scopes = _options?.CommandScopes;
        if (scopes is not { Count: > 0 })
            return;

        var name = CommandLeafName(command);
        List<CommandScope>? matching = null;
        foreach (var scope in scopes)
        {
            if (CommandNameComparer.Equals(scope.Name, name) || CommandNameComparer.Equals(scope.Name, command))
            {
                (matching ??= []).Add(scope);
            }
        }

        // No scope governs this command -> it must be a legacy AllowedCommands entry (any args allowed).
        if (matching is null)
            return;

        foreach (var scope in matching)
        {
            if (scope.ArgumentsAllowed(args))
                return;
        }

        throw new UnauthorizedAccessException(
            $"Arguments for command '{command}' are not permitted by the configured command scope");
    }

    private static string CommandLeafName(string command)
    {
        var leaf = Path.GetFileName(command);
        return string.IsNullOrEmpty(leaf) ? command : leaf;
    }

    internal static void ValidateArguments(string[] args)
    {
        foreach (var arg in args)
        {
            foreach (var prefix in DangerousPrefixes)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Argument '{arg}' is blocked for security.");
            }

            if (_options?.DenyArgPrefixes is { Count: > 0 } deny)
            {
                foreach (var d in deny)
                {
                    if (arg.StartsWith(d, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Argument '{arg}' is blocked by policy.");
                }
            }
        }
    }

    /// <summary>Parses and validates the JSON arg array, returning a (possibly empty) string[].</summary>
    internal static string[] ParseArgs(string argsJson)
    {
        if (string.IsNullOrEmpty(argsJson) || argsJson == "{}")
            return [];

        return JsonSerializer.Deserialize(argsJson, ShellJsonContext.Default.StringArray) ?? [];
    }

    /// <summary>Applies the working-directory and environment-scrubbing policy to a process about to start.</summary>
    internal static void ApplyProcessPolicy(ProcessStartInfo psi)
    {
        var options = _options;
        if (options is null) return;

        if (!string.IsNullOrEmpty(options.WorkingDirectory))
            psi.WorkingDirectory = options.WorkingDirectory;

        if (!options.InheritEnvironment)
        {
            psi.Environment.Clear();
            return;
        }

        if (options.ScrubEnvironmentVariables is { Count: > 0 } scrub)
        {
            var toRemove = new List<string>();
            foreach (var key in psi.Environment.Keys)
            {
                foreach (var marker in scrub)
                {
                    if (key.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(key);
                        break;
                    }
                }
            }
            foreach (var key in toRemove)
                psi.Environment.Remove(key);
        }
    }

    // Retained for callers that build a psi externally (spawn/pty share this).
    internal static void PopulateArguments(ProcessStartInfo psi, string[] args)
    {
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
    }
}

internal record ProcessOutput(string Stdout, string Stderr, int ExitCode);

internal record KillResult(bool Success, string? Error);

internal record ProcessMetrics(int Pid, long Added, long Flushed, long Dropped);

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
[System.Text.Json.Serialization.JsonSerializable(typeof(string[]))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ProcessOutput))]
[System.Text.Json.Serialization.JsonSerializable(typeof(KillResult))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<ProcessMetrics>))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
internal partial class ShellJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
