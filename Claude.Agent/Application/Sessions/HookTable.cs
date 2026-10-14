using System.Collections.Immutable;

namespace Claude.Agent;

// One callback id per registration, hook_{i} in registration order; the initialize config groups
// the ids by (event, matcher) in first-seen order, with the longest timeout of the group.
internal sealed class HookTable
{
    private readonly Dictionary<string, HookRegistration> _byCallbackId;
    private readonly OrderedDictionary<HookEvent, ImmutableArray<HookMatcherConfig>>? _initializeConfig;

    public HookTable(IReadOnlyList<HookRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        _byCallbackId = new Dictionary<string, HookRegistration>(registrations.Count, StringComparer.Ordinal);
        var groups = new OrderedDictionary<(HookEvent Event, string? Matcher), Group>();
        for (var i = 0; i < registrations.Count; i++)
        {
            var registration = registrations[i];
            var callbackId = $"hook_{i}";
            _byCallbackId.Add(callbackId, registration);

            var key = (registration.Event, registration.Matcher);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new Group();
                groups.Add(key, group);
            }

            group.CallbackIds.Add(callbackId);
            if (registration.Timeout is { } timeout)
            {
                group.TimeoutSeconds = Math.Max(group.TimeoutSeconds ?? 0, timeout.TotalSeconds);
            }
        }

        if (groups.Count == 0)
        {
            return;
        }

        var byEvent = new OrderedDictionary<HookEvent, List<HookMatcherConfig>>();
        foreach (var ((hookEvent, matcher), group) in groups)
        {
            if (!byEvent.TryGetValue(hookEvent, out var configured))
            {
                configured = [];
                byEvent.Add(hookEvent, configured);
            }

            configured.Add(new HookMatcherConfig(matcher, [.. group.CallbackIds], group.TimeoutSeconds));
        }

        _initializeConfig = new OrderedDictionary<HookEvent, ImmutableArray<HookMatcherConfig>>(byEvent.Count);
        foreach (var (hookEvent, configured) in byEvent)
        {
            _initializeConfig.Add(hookEvent, [.. configured]);
        }
    }

    public bool Any => _byCallbackId.Count != 0;

    public IReadOnlyDictionary<HookEvent, ImmutableArray<HookMatcherConfig>>? ToInitializeConfig() => _initializeConfig;

    public HookRegistration? Find(string callbackId)
    {
        ArgumentNullException.ThrowIfNull(callbackId);
        return _byCallbackId.GetValueOrDefault(callbackId);
    }

    private sealed class Group
    {
        public List<string> CallbackIds { get; } = [];

        public double? TimeoutSeconds { get; set; }
    }
}
