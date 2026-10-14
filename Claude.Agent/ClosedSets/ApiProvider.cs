namespace Claude.Agent;

/// <summary>The backend an account's requests are served by. The binary's own login applies to the first-party one alone; against any other, credentials come from outside it. A provider this library does not name is read as it is.</summary>
public readonly struct ApiProvider : IEquatable<ApiProvider>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ApiProvider(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>firstParty</c>: Anthropic's own API.</summary>
    public static ApiProvider FirstParty { get; } = new("firstParty");

    /// <summary><c>bedrock</c>: Amazon Bedrock.</summary>
    public static ApiProvider Bedrock { get; } = new("bedrock");

    /// <summary><c>vertex</c>: Google Vertex AI.</summary>
    public static ApiProvider Vertex { get; } = new("vertex");

    /// <summary><c>foundry</c>: the Foundry backend.</summary>
    public static ApiProvider Foundry { get; } = new("foundry");

    /// <summary><c>anthropicAws</c>: Anthropic's API on AWS.</summary>
    public static ApiProvider AnthropicAws { get; } = new("anthropicAws");

    /// <summary><c>anthropicGoogleCloud</c>: Anthropic's API on Google Cloud.</summary>
    public static ApiProvider AnthropicGoogleCloud { get; } = new("anthropicGoogleCloud");

    /// <summary><c>mantle</c>: the Mantle backend.</summary>
    public static ApiProvider Mantle { get; } = new("mantle");

    /// <summary><c>gateway</c>: an enterprise gateway the binary is signed in to.</summary>
    public static ApiProvider Gateway { get; } = new("gateway");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "firstParty" or "bedrock" or "vertex" or "foundry" or "anthropicAws" or "anthropicGoogleCloud" or
        "mantle" or "gateway";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ApiProvider left, ApiProvider right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ApiProvider left, ApiProvider right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ApiProvider(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ApiProvider other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ApiProvider"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ApiProvider other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
