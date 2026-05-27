using Microsoft.Extensions.Logging;

namespace Ryn.Ipc;

internal sealed partial class LoggingIpcObserver(ILogger<RynCommandDispatcher> logger) : IIpcObserver
{
    public void OnCommandStarted(string command) =>
        Log.CommandStarted(logger, command);

    public void OnCommandCompleted(string command, long elapsedMs) =>
        Log.CommandCompleted(logger, command, elapsedMs);

    public void OnCommandFailed(string command, long elapsedMs, Exception exception) =>
        Log.CommandFailed(logger, command, elapsedMs, exception);

    public void OnCommandDenied(string command) =>
        Log.CommandDenied(logger, command);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "IPC command started: {Command}")]
        public static partial void CommandStarted(ILogger logger, string command);

        [LoggerMessage(Level = LogLevel.Debug, Message = "IPC command completed: {Command} ({ElapsedMs}ms)")]
        public static partial void CommandCompleted(ILogger logger, string command, long elapsedMs);

        [LoggerMessage(Level = LogLevel.Warning, Message = "IPC command failed: {Command} ({ElapsedMs}ms)")]
        public static partial void CommandFailed(ILogger logger, string command, long elapsedMs, Exception exception);

        [LoggerMessage(Level = LogLevel.Warning, Message = "IPC command denied: {Command}")]
        public static partial void CommandDenied(ILogger logger, string command);
    }
}
