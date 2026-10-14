namespace Claude.Agent;

internal static class LiveTransports
{
    private static readonly Lock Gate = new();
    private static readonly List<WeakReference<SubprocessCliTransport>> Live = [];
    private static bool _hooked;

    public static void Add(SubprocessCliTransport transport)
    {
        lock (Gate)
        {
            Live.RemoveAll(reference => !reference.TryGetTarget(out _));
            Live.Add(new WeakReference<SubprocessCliTransport>(transport));

            if (!_hooked)
            {
                _hooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => ReapAll();
                AppDomain.CurrentDomain.UnhandledException += (_, _) => ReapAll();
            }
        }
    }

    public static void Remove(SubprocessCliTransport transport)
    {
        lock (Gate)
        {
            Live.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, transport));
        }
    }

    public static int Count
    {
        get
        {
            lock (Gate)
            {
                return Live.Count(reference => reference.TryGetTarget(out _));
            }
        }
    }

    private static void ReapAll()
    {
        SubprocessCliTransport[] live;
        lock (Gate)
        {
            live = [.. Live.Select(reference => reference.TryGetTarget(out var target) ? target : null).OfType<SubprocessCliTransport>()];
            Live.Clear();
        }

        foreach (var transport in live)
        {
            transport.Reap();
        }
    }
}
