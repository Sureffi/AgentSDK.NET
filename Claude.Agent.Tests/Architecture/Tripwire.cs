using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Architecture;

// The real binary never runs here. A `claude` that only leaves a mark sits first on PATH for the
// whole run, so a session started without naming the fake finds the tripwire where the locator
// would have found the real one, and the run fails on the mark.
//
// The other thing only the end of a run can say: every fixture on disk had its whole stdin written
// back by some replay, which the fixture server compares as it closes. The three verb fixtures
// were recorded through a one-shot query and are replayed through a session, so their stdin is
// pinned through the codec instead (Wire/FrameCodecEncodeTests.cs). A filtered run replays few
// fixtures and is not held to it.
[TestClass]
public static class Tripwire
{
    private static readonly string[] PinnedThroughTheCodec = ["fork-parent", "fork", "resume"];

    private static string? _directory;

    private static string Mark => Path.Combine(_directory!, "ran");

    [AssemblyInitialize]
    public static void Set(TestContext context)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _directory = Directory.CreateTempSubdirectory("claude-agent-tripwire-").FullName;
        var script = Path.Combine(_directory, "claude");
        File.WriteAllText(script, $"#!/bin/sh\necho \"$@\" >> '{Mark}'\nexit 97\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Environment.SetEnvironmentVariable("PATH", _directory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
    }

    [AssemblyCleanup]
    public static void Check()
    {
        var fixtures = Directory.EnumerateFiles(Fixture.Root, "*.stdin.jsonl").Select(file => Path.GetFileName(file)[..^".stdin.jsonl".Length]).ToArray();
        var held = FixtureServer.HeldToTheirStdin.Keys.ToArray();
        if (held.Length > fixtures.Length / 2)
        {
            var unheld = fixtures.Except(held, StringComparer.Ordinal).Except(PinnedThroughTheCodec, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            Assert.AreEqual(0, unheld.Length, $"no replay wrote the whole recorded stdin of: {string.Join(", ", unheld)}");
        }

        if (_directory is null)
        {
            return;
        }

        var ran = File.Exists(Mark) ? File.ReadAllText(Mark) : null;
        Directory.Delete(_directory, recursive: true);
        Assert.IsNull(ran, $"a test started `claude` off PATH instead of the fake: {ran}");
    }
}
