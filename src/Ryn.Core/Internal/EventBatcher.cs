using System.Text;
using System.Threading.Channels;

namespace Ryn.Core.Internal;

internal sealed class EventBatcher : IDisposable
{
    private readonly IRynWebView _webView;
    private readonly string _eventName;
    private readonly Timer _flushTimer;
    private readonly Channel<string> _channel;
    private readonly Lock _flushLock = new();
    private bool _disposed;
    private long _droppedCount;

    private const int FlushIntervalMs = 16;
    private const int MaxBatchSize = 100;
    internal const int DefaultCapacity = 10_000;

    internal EventBatcher(IRynWebView webView, string eventName, int capacity = DefaultCapacity)
    {
        _webView = webView;
        _eventName = eventName;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _flushTimer = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
    }

    internal long DroppedCount => Interlocked.Read(ref _droppedCount);

    internal void Add(string jsonData)
    {
        if (_disposed) return;

        if (!_channel.Writer.TryWrite(jsonData))
            Interlocked.Increment(ref _droppedCount);
    }

    internal void FlushNow()
    {
        lock (_flushLock)
        {
            FlushLocked();
        }
    }

    private void Flush()
    {
        lock (_flushLock)
        {
            if (_disposed) return;
            FlushLocked();
        }
    }

    private void FlushLocked()
    {
        var items = new List<string>();
        while (items.Count < MaxBatchSize && _channel.Reader.TryRead(out var item))
            items.Add(item);

        if (items.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(items[i]);
        }
        sb.Append(']');

        _webView.EmitEvent(_eventName, sb.ToString());
    }

    public void Dispose()
    {
        lock (_flushLock)
        {
            if (_disposed) return;
            _disposed = true;
            _flushTimer.Dispose();
            _channel.Writer.Complete();
            FlushLocked();
        }
    }
}
