using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

public static class ShellCommands
{
    private static ShellOptions? _options;

    internal static void Configure(ShellOptions options) => _options = options;

    [RynCommand("shell.execute")]
    public static string Execute(string command, string argsJson)
    {
        var options = _options;
        if (options is not null && options.AllowedCommands.Count > 0)
        {
            var cmdName = Path.GetFileName(command);
            if (!options.AllowedCommands.Contains(cmdName, StringComparer.OrdinalIgnoreCase)
                && !options.AllowedCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Command '{command}' is not in the allowed list");
            }
        }
        else if (options is not null && options.AllowedCommands.Count == 0)
        {
            throw new UnauthorizedAccessException("Shell execution is disabled (no commands in allowlist)");
        }

        var args = string.Empty;
        if (!string.IsNullOrEmpty(argsJson) && argsJson != "{}")
        {
            // Parse JSON array of strings
            var argsArray = JsonSerializer.Deserialize<string[]>(argsJson);
            if (argsArray is not null)
                args = string.Join(' ', argsArray.Select(a => a.Contains(' ', StringComparison.Ordinal) ? $"\"{a}\"" : a));
        }

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {command}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var output = new ProcessOutput(stdout, stderr, process.ExitCode);
        return System.Text.Json.JsonSerializer.Serialize(output, ShellJsonContext.Default.ProcessOutput);
    }

    [RynCommand("shell.open")]
    public static void Open(string url)
    {
        if (OperatingSystem.IsMacOS())
            Process.Start("open", url);
        else if (OperatingSystem.IsLinux())
            Process.Start("xdg-open", url);
        else if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}

internal record ProcessOutput(string Stdout, string Stderr, int ExitCode);

[System.Text.Json.Serialization.JsonSerializable(typeof(ProcessOutput))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
internal partial class ShellJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
