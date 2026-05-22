namespace Ryn.Ipc;

public sealed class RynCommandDispatcher
{
    private readonly ICommandRouter[] _routers;
    private readonly IServiceProvider _services;

    public RynCommandDispatcher(IEnumerable<ICommandRouter> routers, IServiceProvider services)
    {
        _routers = routers.ToArray();
        _services = services;
    }

    public async ValueTask<string> DispatchAsync(
        string command,
        ReadOnlyMemory<byte> args,
        CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < _routers.Length; i++)
        {
            if (_routers[i].CanRoute(command))
                return await _routers[i].RouteAsync(command, args, _services, cancellationToken)
                    .ConfigureAwait(false);
        }

        throw new RynCommandNotFoundException(command);
    }
}
