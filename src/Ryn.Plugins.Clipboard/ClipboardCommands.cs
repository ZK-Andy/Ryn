using System.Diagnostics;
using Ryn.Ipc;

namespace Ryn.Plugins.Clipboard;

public static class ClipboardCommands
{
    [RynCommand("clipboard.readText")]
    public static string ReadText()
    {
        if (OperatingSystem.IsMacOS())
            return RunProcess("pbpaste", "");

        if (OperatingSystem.IsLinux())
            return RunProcess("xclip", "-selection clipboard -o");

        if (OperatingSystem.IsWindows())
            return RunProcess("powershell", "-command Get-Clipboard");

        return string.Empty;
    }

    [RynCommand("clipboard.writeText")]
    public static void WriteText(string text)
    {
        if (OperatingSystem.IsMacOS())
            RunProcessWithInput("pbcopy", "", text);
        else if (OperatingSystem.IsLinux())
            RunProcessWithInput("xclip", "-selection clipboard", text);
        else if (OperatingSystem.IsWindows())
            RunProcess("powershell", $"-command Set-Clipboard -Value '{text.Replace("'", "''", StringComparison.Ordinal)}'");
    }

    [RynCommand("clipboard.hasText")]
    public static bool HasText()
    {
        var text = ReadText();
        return !string.IsNullOrEmpty(text);
    }

    private static string RunProcess(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null) return string.Empty;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static void RunProcessWithInput(string fileName, string arguments, string input)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null) return;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.WaitForExit();
    }
}
