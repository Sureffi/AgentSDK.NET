using System.Collections.Concurrent;

namespace Claude.Agent;

internal sealed class ControlRequestTracker
{
    private readonly ConcurrentDictionary<string, ControlRequestScope> _inFlight = new(StringComparer.Ordinal);

    public ControlRequestScope Begin(RequestId request)
    {
        var key = request.Value ?? string.Empty;
        var scope = new ControlRequestScope(this, key);

        if (_inFlight.TryRemove(key, out var superseded))
        {
            superseded.CancelAndDispose();
        }

        _inFlight[key] = scope;
        return scope;
    }

    public bool Cancel(RequestId request)
    {
        if (!_inFlight.TryGetValue(request.Value ?? string.Empty, out var scope))
        {
            return false;
        }

        scope.Cancel();
        return true;
    }

    public void CancelAll()
    {
        foreach (var pair in _inFlight)
        {
            if (_inFlight.TryRemove(pair))
            {
                pair.Value.CancelAndDispose();
            }
        }
    }

    internal void Release(string key, ControlRequestScope scope) =>
        _inFlight.TryRemove(new KeyValuePair<string, ControlRequestScope>(key, scope));
}

internal sealed class ControlRequestScope : IDisposable
{
    private readonly ControlRequestTracker _tracker;
    private readonly string _key;
    private readonly CancellationTokenSource _source = new();

    private int _cancelled;
    private int _disposed;

    internal ControlRequestScope(ControlRequestTracker tracker, string key)
    {
        _tracker = tracker;
        _key = key;
        Token = _source.Token;
    }

    public CancellationToken Token { get; }

    public bool IsCancelled => Volatile.Read(ref _cancelled) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _tracker.Release(_key, this);
        _source.Dispose();
    }

    internal void Cancel()
    {
        Volatile.Write(ref _cancelled, 1);

        try
        {
            _source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal void CancelAndDispose()
    {
        Cancel();

        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _source.Dispose();
        }
    }
}
