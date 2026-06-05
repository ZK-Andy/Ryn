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
        Console.WriteLine(FormattableString.Invariant($"ryn {RynCliVersion.Current}"));
        return 0;
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
