using System.Diagnostics;
using System.Runtime.InteropServices;
using Ryn.Ipc;

namespace Ryn.Plugins.Dialog;

public static partial class DialogCommands
{
    // Windows MessageBox constants
    private const uint MB_OK = 0x00000000;
    private const uint MB_YESNO = 0x00000004;
    private const uint MB_ICONQUESTION = 0x00000020;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const int IDYES = 6;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(nint hWnd, string lpText, string lpCaption, uint uType);

    [RynCommand("dialog.message")]
    public static void Message(string title, string message)
    {
        if (OperatingSystem.IsMacOS())
        {
            Process.Start("osascript", $"-e 'display dialog \"{EscapeAppleScript(message)}\" with title \"{EscapeAppleScript(title)}\" buttons {{\"OK\"}} default button \"OK\"'")?.WaitForExit();
        }
        else if (OperatingSystem.IsWindows())
        {
            MessageBox(0, message, title, MB_OK | MB_ICONINFORMATION);
        }
        else if (OperatingSystem.IsLinux())
        {
            LinuxMessage(title, message);
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

        if (OperatingSystem.IsWindows())
        {
            return MessageBox(0, message, title, MB_YESNO | MB_ICONQUESTION) == IDYES;
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxConfirm(title, message);
        }

        return false;
    }

    private static string EscapeAppleScript(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? FindLinuxDialogTool()
    {
        foreach (var tool in new[] { "zenity", "kdialog" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "which",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add(tool);

                var proc = Process.Start(psi);
                if (proc is null) continue;
                proc.WaitForExit();
                if (proc.ExitCode == 0) return tool;
            }
            catch (InvalidOperationException)
            {
                // which not found or process creation failed — try next tool
            }
        }

        return null;
    }

    private static void LinuxMessage(string title, string message)
    {
        var tool = FindLinuxDialogTool();
        if (tool is null) return;

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            UseShellExecute = false,
        };

        if (tool == "zenity")
        {
            psi.ArgumentList.Add("--info");
            psi.ArgumentList.Add($"--title={title}");
            psi.ArgumentList.Add($"--text={message}");
        }
        else // kdialog
        {
            psi.ArgumentList.Add("--msgbox");
            psi.ArgumentList.Add(message);
            psi.ArgumentList.Add("--title");
            psi.ArgumentList.Add(title);
        }

        Process.Start(psi)?.WaitForExit();
    }

    private static bool LinuxConfirm(string title, string message)
    {
        var tool = FindLinuxDialogTool();
        if (tool is null) return false;

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            UseShellExecute = false,
        };

        if (tool == "zenity")
        {
            psi.ArgumentList.Add("--question");
            psi.ArgumentList.Add($"--title={title}");
            psi.ArgumentList.Add($"--text={message}");
        }
        else // kdialog
        {
            psi.ArgumentList.Add("--yesno");
            psi.ArgumentList.Add(message);
            psi.ArgumentList.Add("--title");
            psi.ArgumentList.Add(title);
        }

        var process = Process.Start(psi);
        if (process is null) return false;
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
