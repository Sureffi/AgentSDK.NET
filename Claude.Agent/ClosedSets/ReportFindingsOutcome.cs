namespace Claude.Agent;

/// <summary>What became of a finding, where the fixes were applied after the review.</summary>
public readonly struct ReportFindingsOutcome : IEquatable<ReportFindingsOutcome>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ReportFindingsOutcome(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>fixed</c>: the code was changed.</summary>
    public static ReportFindingsOutcome Fixed { get; } = new("fixed");

    /// <summary><c>skipped</c>: the finding was left standing.</summary>
    public static ReportFindingsOutcome Skipped { get; } = new("skipped");

    /// <summary><c>no_change_needed</c>: there was nothing to change.</summary>
    public static ReportFindingsOutcome NoChangeNeeded { get; } = new("no_change_needed");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "fixed" or "skipped" or "no_change_needed";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ReportFindingsOutcome left, ReportFindingsOutcome right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ReportFindingsOutcome left, ReportFindingsOutcome right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ReportFindingsOutcome(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ReportFindingsOutcome other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ReportFindingsOutcome"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ReportFindingsOutcome other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
