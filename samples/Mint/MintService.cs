using System.Globalization;
using Claude.Agent;
using Microsoft.Extensions.Hosting;

namespace Mint;

internal sealed record MintPrompt(string Text, bool Resume);

internal sealed class MintService : BackgroundService
{
    private readonly ClaudeAgentClient _client;
    private readonly MintPrompt _prompt;
    private readonly IHostApplicationLifetime _lifetime;

    public MintService(ClaudeAgentClient client, MintPrompt prompt, IHostApplicationLifetime lifetime)
    {
        _client = client;
        _prompt = prompt;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"> {_prompt.Text}");

        try
        {
            // Mint's own --resume is ContinueLatest, which the library writes as the binary's
            // --continue: the newest session of this directory, and the binary picks it.
            var session = _prompt.Resume ? new ClaudeAgentSessionOptions { ContinueLatest = true } : null;

            await foreach (var message in _client.QueryAsync(_prompt.Text, session, stoppingToken))
            {
                Write(message);

                if (message is ResultMessage { IsError: true } result)
                {
                    Fail(result.Result ?? result.Subtype.ToString());
                }
            }
        }
        catch (ClaudeAgentException e)
        {
            Fail(e.Message);
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private static void Write(AgentMessage message)
    {
        switch (message)
        {
            case SystemInitMessage init:
                Console.WriteLine($"[init] model {init.Model} session {init.SessionId}");
                break;
            case AssistantMessage assistant when assistant.Text.Length > 0:
                Console.WriteLine(assistant.Text);
                break;

            // The model thinks before it answers: one frame of thinking blocks, then the frame
            // with the text. Mint is prompt in, answer out, so the first is passed over.
            case AssistantMessage:
                break;
            case ResultMessage result:
                Console.WriteLine(
                    $"[result] {result.Subtype} turns {result.TurnCount} cost {Cost(result.TotalCostUsd)}");
                break;
            case SystemMessage system:
                Console.WriteLine($"[{system.Subtype}]");
                break;
            default:
                Console.WriteLine($"[{message.GetType().Name}]");
                break;
        }
    }

    private static string Cost(decimal? usd) =>
        usd is null ? "unreported" : "$" + usd.Value.ToString("F4", CultureInfo.InvariantCulture);

    private static void Fail(string message)
    {
        Console.Error.WriteLine($"[error] {message}");
        Environment.ExitCode = 1;
    }
}
