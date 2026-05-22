namespace Ryn.Ipc;

public sealed class RynCommandNotFoundException : Exception
{
    public RynCommandNotFoundException() { }

    public RynCommandNotFoundException(string command)
        : base($"No handler found for command '{command}'")
    {
        Command = command;
    }

    public RynCommandNotFoundException(string command, Exception innerException)
        : base($"No handler found for command '{command}'", innerException)
    {
        Command = command;
    }

    public string? Command { get; }
}
