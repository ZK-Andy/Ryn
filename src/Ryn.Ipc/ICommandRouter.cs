namespace Ryn.Ipc;

public interface ICommandRouter
{
    public bool CanRoute(string command);

    public ValueTask<string> RouteAsync(
        string command,
        ReadOnlyMemory<byte> args,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
