using System.Diagnostics;

namespace Ryn.Core.Internal;

internal static class DeepLinkHandler
{
    internal static Uri? CheckStartupArgs(IList<string> schemes)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
        {
            if (!Uri.TryCreate(args[i], UriKind.Absolute, out var uri)) continue;
            foreach (var scheme in schemes)
            {
                if (uri.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase))
                    return uri;
            }
        }
        return null;
    }

    internal static void RegisterScheme(string scheme, string appName)
    {
        if (OperatingSystem.IsWindows())
            RegisterWindows(scheme, appName);
        else if (OperatingSystem.IsLinux())
            RegisterLinux(scheme, appName);
    }

    private static void RegisterWindows(string scheme, string appName)
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;

        try
        {
            var psi = new ProcessStartInfo("reg")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("add");
            psi.ArgumentList.Add($@"HKCU\Software\Classes\{scheme}");
            psi.ArgumentList.Add("/ve");
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add($"URL:{appName}");
            psi.ArgumentList.Add("/f");
            Process.Start(psi)?.WaitForExit(5000);

            var psi2 = new ProcessStartInfo("reg")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psi2.ArgumentList.Add("add");
            psi2.ArgumentList.Add($@"HKCU\Software\Classes\{scheme}");
            psi2.ArgumentList.Add("/v");
            psi2.ArgumentList.Add("URL Protocol");
            psi2.ArgumentList.Add("/d");
            psi2.ArgumentList.Add("");
            psi2.ArgumentList.Add("/f");
            Process.Start(psi2)?.WaitForExit(5000);

            var psi3 = new ProcessStartInfo("reg")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psi3.ArgumentList.Add("add");
            psi3.ArgumentList.Add($@"HKCU\Software\Classes\{scheme}\shell\open\command");
            psi3.ArgumentList.Add("/ve");
            psi3.ArgumentList.Add("/d");
            psi3.ArgumentList.Add($"\"{exePath}\" \"%1\"");
            psi3.ArgumentList.Add("/f");
            Process.Start(psi3)?.WaitForExit(5000);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void RegisterLinux(string scheme, string appName)
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;

        try
        {
            var desktopDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "applications");
            Directory.CreateDirectory(desktopDir);

            var desktop = $"""
                [Desktop Entry]
                Name={appName}
                Exec={exePath} %u
                Type=Application
                MimeType=x-scheme-handler/{scheme};
                NoDisplay=true
                """;
            var desktopPath = Path.Combine(desktopDir, $"{appName}-{scheme}.desktop");
            File.WriteAllText(desktopPath, desktop);

            var psi = new ProcessStartInfo("xdg-mime")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("default");
            psi.ArgumentList.Add($"{appName}-{scheme}.desktop");
            psi.ArgumentList.Add($"x-scheme-handler/{scheme}");
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        catch (IOException) { }
    }
}
