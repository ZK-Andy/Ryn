namespace Ryn.Ipc;

public sealed class RynCommandDeniedException : Exception
{
    public RynCommandDeniedException() { }

    public RynCommandDeniedException(string command, string reason)
        : base($"Permission denied for '{command}': {reason}")
    {
        Command = command;
        Reason = reason;
    }

    public RynCommandDeniedException(string message) : base(message) { }

    public RynCommandDeniedException(string message, Exception innerException) : base(message, innerException) { }

    public string? Command { get; }
    public string? Reason { get; }
}
