namespace Claude.Agent;

/// <summary>Why an instructions file was loaded. A reason this library does not name is read as it is.</summary>
public readonly struct InstructionsLoadReason : IEquatable<InstructionsLoadReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public InstructionsLoadReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>session_start</c>: the session opened.</summary>
    public static InstructionsLoadReason SessionStart { get; } = new("session_start");

    /// <summary><c>nested_traversal</c>: the binary walked into a directory that has its own.</summary>
    public static InstructionsLoadReason NestedTraversal { get; } = new("nested_traversal");

    /// <summary><c>path_glob_match</c>: a file it applies to was touched.</summary>
    public static InstructionsLoadReason PathGlobMatch { get; } = new("path_glob_match");

    /// <summary><c>include</c>: another instructions file pulled it in.</summary>
    public static InstructionsLoadReason Include { get; } = new("include");

    /// <summary><c>compact</c>: the conversation was compacted and the instructions were read again.</summary>
    public static InstructionsLoadReason Compact { get; } = new("compact");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "session_start" or "nested_traversal" or "path_glob_match" or "include" or "compact";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(InstructionsLoadReason left, InstructionsLoadReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(InstructionsLoadReason left, InstructionsLoadReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator InstructionsLoadReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(InstructionsLoadReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="InstructionsLoadReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is InstructionsLoadReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
