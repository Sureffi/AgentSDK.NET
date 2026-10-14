namespace Claude.Agent;

/// <summary>What a refusal was about. A category this library does not name is read as it is.</summary>
public readonly struct RefusalCategory : IEquatable<RefusalCategory>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public RefusalCategory(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>cyber</c>: offensive computer security.</summary>
    public static RefusalCategory Cyber { get; } = new("cyber");

    /// <summary><c>bio</c>: biological weapons.</summary>
    public static RefusalCategory Bio { get; } = new("bio");

    /// <summary><c>frontier_llm</c>: building a frontier model.</summary>
    public static RefusalCategory FrontierLargeLanguageModel { get; } = new("frontier_llm");

    /// <summary><c>reasoning_extraction</c>: extracting the model's own reasoning.</summary>
    public static RefusalCategory ReasoningExtraction { get; } = new("reasoning_extraction");

    /// <summary><c>general_harms</c>: harm the other categories do not name.</summary>
    public static RefusalCategory GeneralHarms { get; } = new("general_harms");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "cyber" or "bio" or "frontier_llm" or "reasoning_extraction" or "general_harms";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(RefusalCategory left, RefusalCategory right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(RefusalCategory left, RefusalCategory right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator RefusalCategory(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(RefusalCategory other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="RefusalCategory"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is RefusalCategory other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
