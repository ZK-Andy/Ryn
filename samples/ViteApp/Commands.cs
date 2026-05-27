using System.Globalization;
using Ryn.Ipc;

namespace ViteApp;

internal static class ViteAppCommands
{
    [RynCommand]
    public static string Ping() => "pong";

    [RynCommand]
    public static string GetTime() => DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    [RynCommand]
    public static int Add(int a, int b) => a + b;
}
