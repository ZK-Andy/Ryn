using System.Diagnostics;
using System.Text.Json;
using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

/// <summary>
/// Holds a single application's shell allowlist and argument/process policy, resolved from DI as a per-app
/// singleton. Previously this state lived in process-global statics on <see cref="ShellCommands"/>, which two
/// windows or hosts in the same process would clobber; keeping it on an injected instance lets each app run
/// with its own <see cref="ShellOptions"/>. <see cref="ShellCommands"/>, <see cref="SpawnCommands"/>, and
/// <see cref="PtyCommands"/> all funnel their invocations through this one choke point.
/// </summary>
public sealed class ShellExecutionPolicy
{
    private readonly ShellOptions _options;
    private readonly Dictionary<string, string> _resolvedBareCommands;
    private readonly HashSet<string> _allowedFullPaths;

    private static readonly string[] DefaultOpenSchemes = ["http", "https", "mailto"];

    private static readonly string[] DangerousPrefixes =
        ["--proxy", "--socks", "--upload-file", "--data-binary"];

    private static readonly StringComparer CommandNameComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public ShellExecutionPolicy(ShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        // Resolve both legacy allow-list names AND the names of per-argument command scopes, so a
        // scoped command is launchable but still argument-checked.
        var names = new List<string>(options.AllowedCommands);
        foreach (var scope in options.CommandScopes)
            names.Add(scope.Name);
        (_resolvedBareCommands, _allowedFullPaths) = ResolveAllowlist(names);
    }

    internal ShellOptions Options => _options;

    /// <summary>Maximum wall-clock time <c>shell.execute</c> waits before killing the process tree (≤0 = no timeout).</summary>
    internal TimeSpan ExecuteTimeout => _options.ExecuteTimeout;

    /// <summary>Per-stream character cap on captured <c>shell.execute</c> output (≤0 = unbounded).</summary>
    internal int MaxExecuteOutputChars => _options.MaxExecuteOutputChars;

    /// <summary>
    /// Validates that <paramref name="url"/> is an absolute URL whose scheme is in the allowlist.
    /// Rejects bare paths, <c>file://</c>, and any scheme not explicitly permitted — this is what stops
    /// <c>shell.open</c> from launching arbitrary executables, <c>.app</c> bundles, or local files.
    /// </summary>
    internal void ValidateOpenTarget(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new UnauthorizedAccessException($"shell.open target '{url}' is not an absolute URL");

        var allowed = _options.AllowedOpenSchemes is { Count: > 0 } configured
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
    internal string ValidateInvocation(string command, string[] args)
    {
        var resolved = ValidateAndResolveCommand(command);
        EnforceArgumentPolicy(command, args);
        return resolved;
    }

    internal string ValidateAndResolveCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Fail closed: an unconfigured shell never runs anything.
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

    /// <summary>
    /// Enforces, in order: the built-in argument denylist, any integrator deny-prefixes, and — when the
    /// command has one or more <see cref="CommandScope"/>s — that the argv matches a scope exactly.
    /// </summary>
    internal void EnforceArgumentPolicy(string command, string[] args)
    {
        ValidateArguments(args);

        var scopes = _options.CommandScopes;
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

    internal void ValidateArguments(string[] args)
    {
        foreach (var arg in args)
        {
            foreach (var prefix in DangerousPrefixes)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Argument '{arg}' is blocked for security.");
            }

            if (_options.DenyArgPrefixes is { Count: > 0 } deny)
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
    internal void ApplyProcessPolicy(ProcessStartInfo psi)
    {
        var options = _options;

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
