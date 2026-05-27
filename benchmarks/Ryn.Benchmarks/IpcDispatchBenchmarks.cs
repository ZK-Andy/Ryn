using System.Text;
using BenchmarkDotNet.Attributes;
using Ryn.Ipc;

namespace Ryn.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class IpcDispatchBenchmarks
{
    private RynCommandDispatcher _dispatcher = null!;
    private ReadOnlyMemory<byte> _emptyArgs;
    private ReadOnlyMemory<byte> _primitiveArgs;

    [GlobalSetup]
    public void Setup()
    {
        var router = new BenchmarkRouter();
        var capabilities = RynCapabilities.AllowAll();
        var services = new MinimalServiceProvider();

        _dispatcher = new RynCommandDispatcher(
            [router],
            services,
            capabilities);

        _emptyArgs = Encoding.UTF8.GetBytes("{}");
        _primitiveArgs = Encoding.UTF8.GetBytes("""{"name":"bench","count":42}""");
    }

    [Benchmark(Description = "Dispatch: empty args")]
    public ValueTask<string> DispatchEmptyArgs()
    {
        return _dispatcher.DispatchAsync("bench.echo", _emptyArgs);
    }

    [Benchmark(Description = "Dispatch: primitive args")]
    public ValueTask<string> DispatchPrimitiveArgs()
    {
        return _dispatcher.DispatchAsync("bench.echo", _primitiveArgs);
    }

    [Benchmark(Description = "Dispatch: command lookup miss then hit")]
    public ValueTask<string> DispatchWithFallthrough()
    {
        return _dispatcher.DispatchAsync("bench.slow", _emptyArgs);
    }

    private sealed class BenchmarkRouter : ICommandRouter
    {
        private static readonly string[] KnownCommands = ["bench.echo", "bench.slow"];

        public bool CanRoute(string command)
        {
            for (var i = 0; i < KnownCommands.Length; i++)
            {
                if (string.Equals(command, KnownCommands[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public ValueTask<string> RouteAsync(
            string command,
            ReadOnlyMemory<byte> args,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            return new ValueTask<string>("\"ok\"");
        }
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
