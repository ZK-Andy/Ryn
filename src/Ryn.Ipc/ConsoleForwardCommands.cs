using Microsoft.Extensions.Logging;

namespace Ryn.Ipc;

#pragma warning disable CA1812 // Instantiated by generated DI code
internal sealed partial class ConsoleForwardCommands
#pragma warning restore CA1812
{
    private readonly ILogger<ConsoleForwardCommands> _logger;

    public ConsoleForwardCommands(ILogger<ConsoleForwardCommands> logger)
        => _logger = logger;

    [RynCommand("__ryn.console")]
    public void Log(string level, string message)
    {
        switch (level)
        {
            case "error":
                ConsoleLog.Error(_logger, message);
                break;
            case "warn":
                ConsoleLog.Warning(_logger, message);
                break;
            case "info":
                ConsoleLog.Info(_logger, message);
                break;
            default:
                ConsoleLog.Info(_logger, message);
                break;
        }
    }

    private static partial class ConsoleLog
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "[WebView] {Message}")]
        public static partial void Error(ILogger logger, string message);

        [LoggerMessage(Level = LogLevel.Warning, Message = "[WebView] {Message}")]
        public static partial void Warning(ILogger logger, string message);

        [LoggerMessage(Level = LogLevel.Information, Message = "[WebView] {Message}")]
        public static partial void Info(ILogger logger, string message);
    }
}
