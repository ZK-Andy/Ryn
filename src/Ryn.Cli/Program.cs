using System.Reflection;

namespace Ryn.Cli;

internal static class Program
{
    internal static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        return args[0] switch
        {
            "new" => Commands.NewCommand.Execute(args.AsSpan(1)),
            "dev" => Commands.DevCommand.Execute(args.AsSpan(1)),
            "build" => Commands.BuildCommand.Execute(args.AsSpan(1)),
            "bundle" => Commands.BundleCommand.Execute(args.AsSpan(1)),
            "doctor" => Commands.DoctorCommand.Execute(args.AsSpan(1)),
            "--version" or "-v" => HandleVersion(),
            "--help" or "-h" => HandleHelp(),
            _ => HandleUnknown(args[0]),
        };
    }

    private static int HandleVersion()
    {
        Console.WriteLine(FormattableString.Invariant($"ryn {GetInformationalVersion()}"));
        return 0;
    }

    /// <summary>
    /// Returns the real product version. Under MinVer the <c>AssemblyVersion</c> is stamped as
    /// <c>{Major}.0.0.0</c> (i.e. <c>0.0.0.0</c> for a 0.x release), so reading <c>GetName().Version</c>
    /// would always print <c>0.0.0.0</c>. The full SemVer lives in
    /// <see cref="AssemblyInformationalVersionAttribute"/>; we strip the trailing <c>+&lt;commit&gt;</c>
    /// build-metadata that the SDK appends.
    /// </summary>
    private static string GetInformationalVersion()
    {
        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
            return typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? informational[..plus] : informational;
    }

    private static int HandleHelp()
    {
        PrintUsage();
        return 0;
    }

    private static int HandleUnknown(string command)
    {
        Console.Error.WriteLine(FormattableString.Invariant($"Unknown command: {command}"));
        Console.Error.WriteLine("Run 'ryn --help' for usage.");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Ryn CLI — Rich Yet Native

            Usage: ryn <command> [options]

            Commands:
              new <name>    Create a new Ryn project
              dev           Run in development mode with hot reload
              build         Build for production
              bundle        Package into platform installer
              doctor        Check development environment

            Options:
              -h, --help       Show help
              -v, --version    Show version
            """);
    }
}
