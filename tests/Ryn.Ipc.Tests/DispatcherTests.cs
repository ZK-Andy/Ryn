using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ryn.Ipc.Tests;

public sealed class DispatcherTests
{
    [Fact]
    public async Task Dispatch_UnknownCommand_Throws()
    {
        var dispatcher = CreateDispatcher();

        var act = () => dispatcher.DispatchAsync("nonexistent", ReadOnlyMemory<byte>.Empty).AsTask();

        await act.Should().ThrowAsync<RynCommandNotFoundException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public async Task Dispatch_RoutesToCorrectRouter()
    {
        var router = new TestRouter("test", "\"hello\"");
        var dispatcher = CreateDispatcher(router);

        var result = await dispatcher.DispatchAsync("test", ReadOnlyMemory<byte>.Empty);

        result.Should().Be("\"hello\"");
    }

    [Fact]
    public async Task Dispatch_MultipleRouters_FindsCorrectOne()
    {
        var router1 = new TestRouter("cmd1", "\"result1\"");
        var router2 = new TestRouter("cmd2", "\"result2\"");
        var dispatcher = CreateDispatcher(router1, router2);

        var result = await dispatcher.DispatchAsync("cmd2", ReadOnlyMemory<byte>.Empty);

        result.Should().Be("\"result2\"");
    }

    [Fact]
    public async Task Dispatch_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var router = new CancellationTrackingRouter();
        var dispatcher = CreateDispatcher(router);

        await dispatcher.DispatchAsync("tracked", ReadOnlyMemory<byte>.Empty, cts.Token);

        router.ReceivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Dispatch_PassesArgsToRouter()
    {
        var router = new ArgsTrackingRouter();
        var dispatcher = CreateDispatcher(router);

        var args = Encoding.UTF8.GetBytes("{\"x\":42}");
        await dispatcher.DispatchAsync("tracked", args);

        Encoding.UTF8.GetString(router.ReceivedArgs.Span).Should().Be("{\"x\":42}");
    }

    private static RynCommandDispatcher CreateDispatcher(params ICommandRouter[] routers)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new RynCommandDispatcher(routers, services);
    }

    private sealed class TestRouter(string command, string result) : ICommandRouter
    {
        public bool CanRoute(string cmd) => cmd == command;

        public ValueTask<string> RouteAsync(
            string cmd, ReadOnlyMemory<byte> args, IServiceProvider services, CancellationToken ct) =>
            new(result);
    }

    private sealed class CancellationTrackingRouter : ICommandRouter
    {
        public CancellationToken ReceivedToken { get; private set; }

        public bool CanRoute(string command) => command == "tracked";

        public ValueTask<string> RouteAsync(
            string command, ReadOnlyMemory<byte> args, IServiceProvider services, CancellationToken ct)
        {
            ReceivedToken = ct;
            return new("\"ok\"");
        }
    }

    private sealed class ArgsTrackingRouter : ICommandRouter
    {
        public ReadOnlyMemory<byte> ReceivedArgs { get; private set; }

        public bool CanRoute(string command) => command == "tracked";

        public ValueTask<string> RouteAsync(
            string command, ReadOnlyMemory<byte> args, IServiceProvider services, CancellationToken ct)
        {
            ReceivedArgs = args;
            return new("\"ok\"");
        }
    }
}
