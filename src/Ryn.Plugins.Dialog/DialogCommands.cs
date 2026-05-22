using System.Diagnostics;
using Ryn.Ipc;

namespace Ryn.Plugins.Dialog;

public static class DialogCommands
{
    [RynCommand("dialog.message")]
    public static void Message(string title, string message)
    {
        if (OperatingSystem.IsMacOS())
        {
            Process.Start("osascript", $"-e 'display dialog \"{EscapeAppleScript(message)}\" with title \"{EscapeAppleScript(title)}\" buttons {{\"OK\"}} default button \"OK\"'")?.WaitForExit();
        }
    }

    [RynCommand("dialog.confirm")]
    public static bool Confirm(string title, string message)
    {
        if (OperatingSystem.IsMacOS())
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e 'try\nset result to button returned of (display dialog \"{EscapeAppleScript(message)}\" with title \"{EscapeAppleScript(title)}\" buttons {{\"No\", \"Yes\"}} default button \"Yes\")\nif result is \"Yes\" then\nreturn \"true\"\nelse\nreturn \"false\"\nend if\non error\nreturn \"false\"\nend try'",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            if (process is null) return false;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output == "true";
        }

        return false;
    }

    private static string EscapeAppleScript(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}
