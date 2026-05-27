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
    private long _addedCount;
    private long _flushedCount;

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

    internal long AddedCount => Interlocked.Read(ref _addedCount);
    internal long FlushedCount => Interlocked.Read(ref _flushedCount);
    internal long Backlog => AddedCount - FlushedCount;

    internal void Add(string jsonData)
    {
        if (_disposed) return;
        _channel.Writer.TryWrite(jsonData);
        Interlocked.Increment(ref _addedCount);
    }

    internal void FlushNow()
    {
        lock (_flushLock)
        {
            FlushAllLocked();
        }
    }

    private void Flush()
    {
        lock (_flushLock)
        {
            if (_disposed) return;
            FlushBatchLocked();
        }
    }

    private void FlushBatchLocked()
    {
        var items = new List<string>();
        while (items.Count < MaxBatchSize && _channel.Reader.TryRead(out var item))
            items.Add(item);

        if (items.Count == 0) return;
        EmitBatch(items);
    }

    private void FlushAllLocked()
    {
        var items = new List<string>();
        while (_channel.Reader.TryRead(out var item))
            items.Add(item);

        if (items.Count == 0) return;

        for (var offset = 0; offset < items.Count; offset += MaxBatchSize)
        {
            var count = Math.Min(MaxBatchSize, items.Count - offset);
            EmitBatch(items.GetRange(offset, count));
        }
    }

    private void EmitBatch(List<string> items)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(items[i]);
        }
        sb.Append(']');

        Interlocked.Add(ref _flushedCount, items.Count);
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
            FlushAllLocked();
        }
    }
}
