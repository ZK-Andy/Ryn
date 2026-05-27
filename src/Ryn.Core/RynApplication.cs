using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ryn.Core.Internal;

namespace Ryn.Core;

public sealed partial class RynApplication : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RynApplication> _logger;
    private readonly List<IRynPlugin> _plugins = [];
    private RynWindow? _window;
    private bool _disposed;

    internal RynApplication(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetService<ILogger<RynApplication>>() ?? NullLogger<RynApplication>.Instance;
    }

    public IServiceProvider Services => _services;

    public IRynWindow Window => _window ?? throw new InvalidOperationException("Application is not running");

    public IRynWebView WebView => _window?.WebView ?? throw new InvalidOperationException("Application is not running");

    public static RynApplicationBuilder CreateBuilder() => new(programmaticOptions: null);

    public static RynApplicationBuilder CreateBuilder(RynOptions options) => new(options);

    public ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (OperatingSystem.IsWindows() && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "Ryn requires [STAThread] on the entry point for Windows. " +
                "Use '[STAThread] static void Main()' instead of top-level statements or async Main.");
        }

        Log.Starting(_logger);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        foreach (var plugin in _plugins)
        {
#pragma warning disable CA1849 // Intentional sync-over-async: no event loop exists yet, so no deadlock risk
            plugin.InitializeAsync(cts.Token).AsTask().GetAwaiter().GetResult();
#pragma warning restore CA1849
        }

        var options = _services.GetRequiredService<RynOptions>();
        _window = new RynWindow(options);

        // Wire IPC command dispatcher if registered (before Run, applied during OnReady)
        var commandHandler = _services.GetService<CommandDispatchHandler>();
        if (commandHandler is not null)
        {
            _window.SetCommandHandler(commandHandler);
        }

        var accessor = _services.GetRequiredService<RynWindowAccessor>();
        accessor.Window = _window;

        var nativeAccessor = _services.GetRequiredService<NativeApplicationAccessor>();
        _window.OnNativeReady = handle => nativeAccessor.ApplicationHandle = handle;

        Log.Running(_logger);

        _window.Run(cts.Token);

        Log.ShuttingDown(_logger);

        return ValueTask.CompletedTask;
    }

    public void Run(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2012 // Intentional sync-over-async: convenience wrapper for [STAThread] Main
        RunAsync(cancellationToken).GetAwaiter().GetResult();
#pragma warning restore CA2012
    }

    internal void AddPlugin(IRynPlugin plugin) => _plugins.Add(plugin);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _window?.Dispose();
        _window = null;

        foreach (var plugin in _plugins)
        {
            if (plugin is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (plugin is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        if (_services is IAsyncDisposable serviceDisposable)
        {
            await serviceDisposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Ryn application starting")]
        public static partial void Starting(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Ryn application running")]
        public static partial void Running(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Ryn application shutting down")]
        public static partial void ShuttingDown(ILogger logger);
    }
}
