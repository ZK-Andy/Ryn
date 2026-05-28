using System.Diagnostics;

namespace Ryn.Core.Internal;

internal sealed class SystemThemeDetector : IDisposable
{
    private Timer? _timer;
    private AppTheme _current;
    private bool _disposed;

    internal event Action<AppTheme>? ThemeChanged;

    internal AppTheme Current => _current;

    internal SystemThemeDetector()
    {
        _current = Detect();
    }

    internal void StartPolling(TimeSpan interval)
    {
        _timer = new Timer(_ =>
        {
            if (_disposed) return;
            var theme = Detect();
            if (theme != _current)
            {
                _current = theme;
                ThemeChanged?.Invoke(theme);
            }
        }, null, interval, interval);
    }

    internal static AppTheme Detect()
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                return DetectMacOs();
            if (OperatingSystem.IsWindows())
                return DetectWindows();
            if (OperatingSystem.IsLinux())
                return DetectLinux();
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }

        return AppTheme.Light;
    }

    private static AppTheme DetectMacOs()
    {
        var psi = new ProcessStartInfo("defaults")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("read");
        psi.ArgumentList.Add("-g");
        psi.ArgumentList.Add("AppleInterfaceStyle");

        using var process = Process.Start(psi);
        if (process is null) return AppTheme.Light;

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(2000);
        return output.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? AppTheme.Dark : AppTheme.Light;
    }

    private static AppTheme DetectWindows()
    {
        var psi = new ProcessStartInfo("reg")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("query");
        psi.ArgumentList.Add(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        psi.ArgumentList.Add("/v");
        psi.ArgumentList.Add("AppsUseLightTheme");

        using var process = Process.Start(psi);
        if (process is null) return AppTheme.Light;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);
        return output.Contains("0x0", StringComparison.Ordinal) ? AppTheme.Dark : AppTheme.Light;
    }

    private static AppTheme DetectLinux()
    {
        var psi = new ProcessStartInfo("gsettings")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("get");
        psi.ArgumentList.Add("org.gnome.desktop.interface");
        psi.ArgumentList.Add("color-scheme");

        using var process = Process.Start(psi);
        if (process is null) return AppTheme.Light;

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(2000);
        return output.Contains("dark", StringComparison.OrdinalIgnoreCase) ? AppTheme.Dark : AppTheme.Light;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}
