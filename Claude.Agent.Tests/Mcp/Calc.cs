
namespace Claude.Agent.Tests.Mcp;

internal sealed class Calc
{
    [McpTool("add", "Add two numbers")]
    public static double Add(double a, double b) => a + b;
}
