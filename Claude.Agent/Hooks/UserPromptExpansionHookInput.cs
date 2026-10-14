namespace Claude.Agent;

/// <summary>A slash command or an MCP prompt is being expanded into the prompt the model reads.</summary>
public sealed class UserPromptExpansionHookInput : HookInput
{
    internal UserPromptExpansionHookInput(HookInputCommon common,
        UserPromptExpansionType expansionType,
        string commandName,
        string commandArguments,
        string? commandSource,
        string prompt)
        : base(HookEvent.UserPromptExpansion, common)
    {
        ExpansionType = expansionType;
        CommandName = commandName;
        CommandArguments = commandArguments;
        CommandSource = commandSource;
        Prompt = prompt;
    }

    /// <summary>What is being expanded.</summary>
    public UserPromptExpansionType ExpansionType { get; }

    /// <summary>The command's name.</summary>
    public string CommandName { get; }

    /// <summary>What was typed after the command's name, as one string.</summary>
    public string CommandArguments { get; }

    /// <summary>Where the command is defined; null when the binary did not say.</summary>
    public string? CommandSource { get; }

    /// <summary>The prompt the expansion produced.</summary>
    public string Prompt { get; }
}
