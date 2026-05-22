namespace Ryn.Core;

/// <summary>
/// Handler for dispatching IPC commands from JavaScript to C#.
/// Registered by the Ryn.Ipc package via AddRynCommands().
/// </summary>
public delegate ValueTask<string> CommandDispatchHandler(
    string command,
    ReadOnlyMemory<byte> args,
    CancellationToken cancellationToken);
