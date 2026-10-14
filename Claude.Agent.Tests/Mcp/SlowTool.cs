namespace Claude.Agent.Tests.Mcp;

// The recorder's slow server of the mcp-cancel fixture, the same tool under the same name: it waits
// until its token is cancelled.
internal sealed class SlowTool
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public Task Cancelled => _cancelled.Task;

    [McpTool("wait", "Waits until it is told to stop")]
    public async Task<string> Wait(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return "done";
        }
        catch (OperationCanceledException)
        {
            _cancelled.TrySetResult();
            throw;
        }
    }
}
