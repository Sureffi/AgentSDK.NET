namespace Claude.Agent.Tests.Sessions;

// The read options as the tests spell them: everything, or one page, under one directory.
internal static class Query
{
    public static SessionQueryOptions All(string? cwd = null) => new() { WorkingDirectory = cwd };

    public static SessionQueryOptions Page(int? limit, int offset = 0, string? cwd = null) =>
        new() { WorkingDirectory = cwd, Limit = limit, Offset = offset };
}
