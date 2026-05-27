using System.Globalization;
using Ryn.Ipc;

namespace RynApp;

public static class AppCommands
{
    [RynCommand]
    public static string Greet(string name) => $"Hello, {name}!";

    [RynCommand]
    public static int Add(int a, int b) => a + b;

    [RynCommand]
    public static string GetTime() => DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
