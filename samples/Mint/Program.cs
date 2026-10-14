using Claude.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mint;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);

// `--resume` first: the newest session of this directory is resumed with the prompt that follows.
var resume = args.Length > 0 && args[0] == "--resume";
var words = resume ? args[1..] : args;
builder.Services.AddSingleton(new MintPrompt(
    words.Length > 0 ? string.Join(' ', words) : (resume ? "Reply with exactly the word you replied last time." : "What is a mint?"),
    resume));

// What every call is asked unless it says otherwise; MintService adds which session it is.
builder.Services.AddClaudeAgentClient(o =>
{
    o.Defaults.SystemPrompt = AgentSystemPrompt.FromText("You are Mint. Answer in one short paragraph.");
    o.Defaults.MaxTurns = 1;
    o.Defaults.Model = "haiku";
});
builder.Services.AddHostedService<MintService>();

using var host = builder.Build();
await host.RunAsync();

return Environment.ExitCode;
