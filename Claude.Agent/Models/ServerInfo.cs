namespace Claude.Agent;

/// <summary>What the binary answered the initialize request with: what it loaded, who it is logged in as, and how it is running.</summary>
public sealed class ServerInfo
{
    internal ServerInfo()
    {
    }

    /// <summary>The slash commands loaded.</summary>
    public IReadOnlyList<SlashCommandInfo> Commands { get; internal init; } = [];

    /// <summary>The subagents loaded.</summary>
    public IReadOnlyList<AgentInfo> Agents { get; internal init; } = [];

    /// <summary>The output style in effect.</summary>
    public string? OutputStyle { get; internal init; }

    /// <summary>The output styles to choose from.</summary>
    public IReadOnlyList<string> AvailableOutputStyles { get; internal init; } = [];

    /// <summary>The directory the account's own output styles are read from.</summary>
    public string? UserOutputStylesDirectory { get; internal init; }

    /// <summary>The models the account may choose.</summary>
    public IReadOnlyList<ModelInfo> Models { get; internal init; } = [];

    /// <summary>The account the binary is logged in as.</summary>
    public AccountInfo? Account { get; internal init; }

    /// <summary>Whether the hooks this initialize carried were registered; null when it carried none, or when the binary predates the answer.</summary>
    public bool? HooksApplied { get; internal init; }

    /// <summary>Whether every plugin this initialize listed was already loaded; null when it listed none, or when the binary predates the answer.</summary>
    public bool? PluginsApplied { get; internal init; }

    /// <summary>Whether fast mode is running.</summary>
    public FastModeState? FastModeState { get; internal init; }

    /// <summary>Why fast mode is not available.</summary>
    public FastModeDisabledReason? FastModeDisabledReason { get; internal init; }

    /// <summary>The binary's process id.</summary>
    public long? ProcessId { get; internal init; }

    /// <summary>The mode the session runs under; null when the reply carried none.</summary>
    public PermissionMode? PermissionMode { get; internal init; }

    /// <summary>What the session was doing when it answered; <see cref="Claude.Agent.SessionState.Idle"/> in every recording.</summary>
    public SessionState? SessionState { get; internal init; }

    /// <summary>Whether the binary sends no analytics.</summary>
    public bool? AnalyticsDisabled { get; internal init; }

    /// <summary>Whether remote control may be reached from this session.</summary>
    public bool? RemoteControlAvailable { get; internal init; }

    /// <summary>Whether remote control turns itself on.</summary>
    public bool? RemoteControlAutoEnable { get; internal init; }

    /// <summary>Whether remote control is on unless the account turns it off.</summary>
    public bool? RemoteControlAutoOnByDefault { get; internal init; }

    /// <summary>Whether remote control connects itself unless the account says otherwise.</summary>
    public bool? RemoteControlAutoConnectDefault { get; internal init; }

    /// <summary>Whether an editor extension may turn remote control on by itself.</summary>
    public bool? IdeRemoteControlAutoEnableGate { get; internal init; }

    /// <summary>The capabilities the binary names itself as having, each as it writes it; empty when the reply carried none.</summary>
    public IReadOnlyList<string> Capabilities { get; internal init; } = [];

    /// <summary>The feedback mode the binary reports; null when the reply carried none.</summary>
    public FeedbackMode? FeedbackMode { get; internal init; }
}

/// <summary>The feedback mode the binary reports in its initialize reply.</summary>
public sealed class FeedbackMode
{
    internal FeedbackMode()
    {
    }

    /// <summary>The mode's kind; null when the binary sent none.</summary>
    public FeedbackModeKind? Kind { get; internal init; }
}

/// <summary>One subagent the binary loaded.</summary>
public sealed class AgentInfo
{
    internal AgentInfo(string name)
    {
        Name = name;
    }

    /// <summary>The agent's type, which is the name the Agent tool is called with.</summary>
    public string Name { get; }

    /// <summary>When to use the agent.</summary>
    public string? Description { get; internal init; }

    /// <summary>The model the agent runs on: an alias, a model id, or <c>inherit</c> for the one that started it.</summary>
    public string? Model { get; internal init; }
}

/// <summary>One model the account may choose.</summary>
public sealed class ModelInfo
{
    internal ModelInfo(string value)
    {
        Value = value;
    }

    /// <summary>What to set the model to for this row: an alias such as <c>sonnet</c>, or a model id.</summary>
    public string Value { get; }

    /// <summary>The model id <see cref="Value"/> resolves to.</summary>
    public string? ResolvedModel { get; internal init; }

    /// <summary>The row's name, for a person to read.</summary>
    public string? DisplayName { get; internal init; }

    /// <summary>What the model is for.</summary>
    public string? Description { get; internal init; }

    /// <summary>Whether the model takes an effort level.</summary>
    public bool? SupportsEffort { get; internal init; }

    /// <summary>The effort levels the model takes.</summary>
    public IReadOnlyList<Effort> SupportedEffortLevels { get; internal init; } = [];

    /// <summary>Whether the model decides for itself when and how much to think.</summary>
    public bool? SupportsAdaptiveThinking { get; internal init; }

    /// <summary>Whether the model runs in fast mode.</summary>
    public bool? SupportsFastMode { get; internal init; }

    /// <summary>Whether the model runs in auto mode.</summary>
    public bool? SupportsAutoMode { get; internal init; }
}

/// <summary>The account the binary is logged in as. Every member is the binary's to send or leave out.</summary>
public sealed class AccountInfo
{
    internal AccountInfo()
    {
    }

    /// <summary>The account's address.</summary>
    public string? Email { get; internal init; }

    /// <summary>The organization the account belongs to.</summary>
    public string? Organization { get; internal init; }

    /// <summary>The account's plan.</summary>
    public string? SubscriptionType { get; internal init; }

    /// <summary>Where the token the binary authenticates with came from.</summary>
    public string? TokenSource { get; internal init; }

    /// <summary>Where the API key the binary authenticates with came from.</summary>
    public ApiKeySource? ApiKeySource { get; internal init; }

    /// <summary>The backend the account's requests are served by.</summary>
    public ApiProvider? ApiProvider { get; internal init; }
}
