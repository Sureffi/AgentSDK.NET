using System.Text.Json;

namespace Claude.Agent;

/// <summary>The account's rate-limit standing, sent when the binary learns it.</summary>
public sealed class RateLimitMessage : AgentMessage
{
    internal RateLimitMessage(RateLimitStatus status, string uuid, string sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Status = status;
    }

    /// <summary>Where the account stands against the limit.</summary>
    public RateLimitStatus Status { get; }

    /// <summary>When the limit resets.</summary>
    public DateTimeOffset? ResetsAt { get; internal init; }

    /// <summary>Which limit this standing is about.</summary>
    public RateLimitType? LimitType { get; internal init; }

    /// <summary>How much of the limit is used, from 0 to 1.</summary>
    public double? Utilization { get; internal init; }

    /// <summary>The standing of overage use, in the same words as <see cref="Status"/>.</summary>
    public RateLimitStatus? OverageStatus { get; internal init; }

    /// <summary>When the overage limit resets.</summary>
    public DateTimeOffset? OverageResetsAt { get; internal init; }

    /// <summary>Why overage is off, when it is.</summary>
    public OverageDisabledReason? OverageDisabledReason { get; internal init; }

    /// <summary>Whether the turn that reported this ran on overage.</summary>
    public bool? IsUsingOverage { get; internal init; }

    /// <summary>Whether the account is on overage now.</summary>
    public bool? OverageInUse { get; internal init; }

    /// <summary>The share of the limit the account has just passed, when it passed one.</summary>
    public double? SurpassedThreshold { get; internal init; }

    /// <summary>What the limit is counted over.</summary>
    public RateLimitScope? LimitScope { get; internal init; }

    /// <summary>What the binary wants done about the limit, when it wants something.</summary>
    public RateLimitErrorCode? ErrorCode { get; internal init; }

    /// <summary>Whether the account may buy credits.</summary>
    public bool? CanUserPurchaseCredits { get; internal init; }

    /// <summary>Whether the account has a payment method that can be charged.</summary>
    public bool? HasChargeableSavedPaymentMethod { get; internal init; }

    /// <summary>Every window the account is measured over, by the name the binary gives it.</summary>
    public IReadOnlyDictionary<string, RateLimitWindow> UnifiedWindows { get; internal init; } = EmptyWindows;

    private static readonly IReadOnlyDictionary<string, RateLimitWindow> EmptyWindows = new Dictionary<string, RateLimitWindow>();
}

/// <summary>One window the account's use is measured over.</summary>
public sealed class RateLimitWindow
{
    internal RateLimitWindow()
    {
    }

    /// <summary>How much of the window is used, from 0 to 1.</summary>
    public double? Utilization { get; internal init; }

    /// <summary>When the window resets.</summary>
    public DateTimeOffset? ResetsAt { get; internal init; }
}
