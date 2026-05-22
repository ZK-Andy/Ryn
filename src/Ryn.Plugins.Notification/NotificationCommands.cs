using System.Diagnostics;
using Ryn.Ipc;

namespace Ryn.Plugins.Notification;

public static class NotificationCommands
{
    [RynCommand("notification.send")]
    public static void Send(string title, string body)
    {
        if (OperatingSystem.IsMacOS())
        {
            var escapedTitle = title.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            var escapedBody = body.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            Process.Start("osascript", $"-e 'display notification \"{escapedBody}\" with title \"{escapedTitle}\"'");
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start("notify-send", $"\"{title}\" \"{body}\"");
        }
        else if (OperatingSystem.IsWindows())
        {
            var script = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; $xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent(0); $xml.GetElementsByTagName('text')[0].AppendChild($xml.CreateTextNode('{title}')); [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Ryn').Show([Windows.UI.Notifications.ToastNotification]::new($xml))";
            Process.Start("powershell", $"-command \"{script}\"");
        }
    }

    [RynCommand("notification.isSupported")]
    public static bool IsSupported()
    {
        if (OperatingSystem.IsMacOS()) return true;
        if (OperatingSystem.IsLinux()) return true;
        if (OperatingSystem.IsWindows()) return true;
        return false;
    }
}
