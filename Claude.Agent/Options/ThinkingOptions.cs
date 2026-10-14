namespace Claude.Agent;

/// <summary>How much the model thinks before answering, and whether the binary shows it.</summary>
public sealed class ThinkingOptions
{
    private ThinkingOptions(int? budgetTokens, bool isDisabled, bool omitThinking)
    {
        BudgetTokens = budgetTokens;
        IsDisabled = isDisabled;
        OmitThinking = omitThinking;
    }

    /// <summary>A fixed thinking budget; null when adaptive or disabled.</summary>
    public int? BudgetTokens { get; }

    /// <summary>True when thinking is off.</summary>
    public bool IsDisabled { get; }

    /// <summary>
    /// Ask the binary to leave the thinking text out. Left false, no display is asked for and the
    /// API's default applies.
    /// </summary>
    public bool OmitThinking { get; }

    /// <summary>The model decides how much to think.</summary>
    public static ThinkingOptions Adaptive(bool omitThinking = false) => new(null, false, omitThinking);

    /// <summary>No thinking.</summary>
    public static ThinkingOptions Disabled() => new(null, true, false);

    /// <summary>Thinking within <paramref name="tokens"/>.</summary>
    public static ThinkingOptions WithBudget(int tokens, bool omitThinking = false) => new(tokens, false, omitThinking);
}
