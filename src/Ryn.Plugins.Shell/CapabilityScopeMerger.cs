using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

internal static class CapabilityScopeMerger
{
    /// <summary>
    /// Merges capability scopes from ryn.json into ShellOptions.
    /// If ryn.json declares command scopes, they become the maximum allowed set.
    /// Programmatic commands that aren't in the declared scope are removed.
    /// If ryn.json doesn't declare scopes, programmatic options apply as-is.
    /// </summary>
    internal static void MergeShellScope(RynCapabilities capabilities, ShellOptions options)
    {
        var scope = capabilities.GetScope("shell");
        if (scope is null || !scope.HasCommandPolicy)
            return;

        // commands: [] means explicit deny-all
        if (scope.AllowedCommands!.Count == 0)
        {
            options.AllowedCommands.Clear();
            return;
        }

        if (options.AllowedCommands.Count == 0)
        {
            options.AllowedCommands.AddRange(scope.AllowedCommands);
            return;
        }

        var allowed = new HashSet<string>(scope.AllowedCommands, StringComparer.OrdinalIgnoreCase);
        var clamped = options.AllowedCommands
            .Where(cmd => allowed.Contains(cmd))
            .ToList();

        options.AllowedCommands.Clear();
        options.AllowedCommands.AddRange(clamped);
    }
}
