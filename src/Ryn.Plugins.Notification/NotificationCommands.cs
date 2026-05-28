using System.Diagnostics;
using Ryn.Ipc;

namespace Ryn.Plugins.Notification;

public static class NotificationCommands
{
    [RynCommand("notification.send")]
    public static void Send(string title, string body)
    {
        SendInternal(title, body, sound: null, iconPath: null);
    }

    [RynCommand("notification.sendWithSound")]
    public static void SendWithSound(string title, string body, string sound)
    {
        SendInternal(title, body, sound, iconPath: null);
    }

    [RynCommand("notification.sendWithIcon")]
    public static void SendWithIcon(string title, string body, string iconPath)
    {
        SendInternal(title, body, sound: null, iconPath);
    }

    [RynCommand("notification.isSupported")]
    public static bool IsSupported()
    {
        if (OperatingSystem.IsMacOS()) return true;
        if (OperatingSystem.IsLinux()) return IsToolAvailable("notify-send");
        if (OperatingSystem.IsWindows()) return true;
        return false;
    }

    [RynCommand("notification.requestPermission")]
    public static string RequestPermission()
    {
        if (OperatingSystem.IsMacOS())
            return "granted"; // osascript notifications always work

        if (OperatingSystem.IsLinux())
            return IsToolAvailable("notify-send") ? "granted" : "denied";

        if (OperatingSystem.IsWindows())
            return "granted"; // PowerShell toast always works

        return "denied";
    }

    private static void SendInternal(string title, string body, string? sound, string? iconPath)
    {
        if (OperatingSystem.IsMacOS())
            SendMacOS(title, body, sound);
        else if (OperatingSystem.IsLinux())
            SendLinux(title, body, iconPath);
        else if (OperatingSystem.IsWindows())
            SendWindows(title, body);
    }

    private static void SendMacOS(string title, string body, string? sound)
    {
        var escapedTitle = EscapeOsascript(title);
        var escapedBody = EscapeOsascript(body);

        var script = $"display notification \"{escapedBody}\" with title \"{escapedTitle}\"";

        if (!string.IsNullOrEmpty(sound))
        {
            var escapedSound = EscapeOsascript(sound);
            script += $" sound name \"{escapedSound}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = "osascript",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);

        RunProcess(psi);
    }

    private static void SendLinux(string title, string body, string? iconPath)
    {
        if (!IsToolAvailable("notify-send"))
            throw new InvalidOperationException(
                "notify-send is not installed. Install libnotify (e.g. 'sudo apt install libnotify-bin') to enable notifications.");

        var psi = new ProcessStartInfo
        {
            FileName = "notify-send",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("--urgency=normal");

        if (!string.IsNullOrEmpty(iconPath))
        {
            psi.ArgumentList.Add($"--icon={iconPath}");
        }

        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(title);
        psi.ArgumentList.Add(body);

        RunProcess(psi);
    }

    private static void SendWindows(string title, string body)
    {
        // Build toast XML with both title and body text nodes.
        // ToastText02 template has two <text> elements: title and body.
        var escapedTitle = EscapePowerShell(title);
        var escapedBody = EscapePowerShell(body);

        var script = string.Concat(
            "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; ",
            "$template = '<toast><visual><binding template=\"ToastText02\">",
            $"<text id=\"1\">{escapedTitle}</text>",
            $"<text id=\"2\">{escapedBody}</text>",
            "</binding></visual></toast>'; ",
            "$xml = [Windows.Data.Xml.Dom.XmlDocument]::new(); ",
            "$xml.LoadXml($template); ",
            "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Ryn').Show(",
            "[Windows.UI.Notifications.ToastNotification]::new($xml))");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        RunProcess(psi);
    }

    private static string EscapeOsascript(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapePowerShell(string value)
    {
        // Escape XML-special characters for embedding in toast XML, then
        // escape single quotes for the PowerShell string wrapper.
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static void RunProcess(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi);
        if (process is null)
            throw new InvalidOperationException($"Failed to start process '{psi.FileName}'.");

        process.WaitForExit();
    }

    private static bool IsToolAvailable(string tool)
    {
        var whichCommand = OperatingSystem.IsWindows() ? "where" : "which";

        var psi = new ProcessStartInfo
        {
            FileName = whichCommand,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(tool);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
