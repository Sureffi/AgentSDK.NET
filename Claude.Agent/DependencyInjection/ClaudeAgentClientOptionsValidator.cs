using Microsoft.Extensions.Options;

namespace Claude.Agent;

// The options checked at host start against `host`, as the client made from them resolves its
// directories.
internal sealed class ClaudeAgentClientOptionsValidator(HostEnvironment host) : IValidateOptions<ClaudeAgentClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ClaudeAgentClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.Validate(host);
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException refused)
        {
            return ValidateOptionsResult.Fail(refused.Message);
        }
    }
}
