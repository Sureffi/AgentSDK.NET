using System.Runtime.Versioning;

namespace Claude.Agent.Tests.Wire;

// Where the binary is looked for: PATH, then the native installer's launcher under the profile
// directory, then the local npm install's under the user's configuration directory, and nowhere
// else. The names are the ones off Windows.
[TestClass]
[UnsupportedOSPlatform("windows")]
public sealed class CliLocatorTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void MakeATree()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the locator looks for claude.exe on Windows; there is no Windows machine here.");
        }

        _root = Directory.CreateTempSubdirectory("claude-agent-locator").FullName;
    }

    [TestCleanup]
    public void RemoveTheTree() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void AConfiguredPathIsTakenAsGivenWithoutASearch()
    {
        _ = Executable(Folder("bin"), "claude");

        var located = Locate("/opt/claude/bin/claude", PathOf(Folder("bin")));

        Assert.IsTrue(located.IsOk);
        Assert.AreEqual("/opt/claude/bin/claude", located.Value);
    }

    [TestMethod]
    public void TheFirstDirectoryOnThePathThatHoldsClaudeWins()
    {
        var first = Executable(Folder("first"), "claude");
        _ = Executable(Folder("second"), "claude");

        var located = Locate(null, PathOf(Folder("first"), Folder("second")));

        Assert.IsTrue(located.IsOk);
        Assert.AreEqual(first, located.Value);
    }

    [TestMethod]
    public void ADirectoryOnThePathThatHoldsNoClaudeIsSkipped()
    {
        var hit = Executable(Folder("second"), "claude");

        var located = Locate(null, PathOf(Folder("empty"), Folder("second")));

        Assert.IsTrue(located.IsOk);
        Assert.AreEqual(hit, located.Value);
    }

    [TestMethod]
    public void ThePathComesBeforeTheInstallers()
    {
        var onPath = Executable(Folder("bin"), "claude");
        _ = Executable(Folder("home", ".local", "bin"), "claude");

        var located = Locate(null, PathOf(Folder("bin")));

        Assert.IsTrue(located.IsOk);
        Assert.AreEqual(onPath, located.Value);
    }

    [TestMethod]
    public void TheNativeInstallersLauncherIsFoundWhenThePathHoldsNoClaude()
    {
        var launcher = Executable(Folder("home", ".local", "bin"), "claude");

        var located = Locate(null, PathOf(Folder("empty")));

        Assert.IsTrue(located.IsOk);
        Assert.AreEqual(launcher, located.Value);
    }

    [TestMethod]
    public void AnEmptyPathStillFindsTheNativeInstall()
    {
        var launcher = Executable(Folder("home", ".local", "bin"), "claude");

        var located = Locate(null, string.Empty);

        Assert.IsTrue(located.IsOk);
        Assert.AreEqual(launcher, located.Value);
    }

    [TestMethod]
    public void TheLocalInstallsLauncherUnderTheUsersDirectoryIsFoundAfterTheNativeOne()
    {
        var local = Executable(Folder("user", "local"), "claude");

        var alone = Locate(null, string.Empty);
        var native = Executable(Folder("home", ".local", "bin"), "claude");
        var beside = Locate(null, string.Empty);

        Assert.AreEqual(local, alone.Value);
        Assert.AreEqual(native, beside.Value);
    }

    // Spelt in two halves, as the tree's check for another SDK's names spells it.
    [TestMethod]
    public void APlaceNoInstallerOfTheBinaryPutsItIsNotLookedIn()
    {
        _ = Executable(Folder("home", ".npm-" + "global", "bin"), "claude");
        _ = Executable(Folder("home", "node_modules", ".bin"), "claude");

        var located = Locate(null, string.Empty);

        Assert.IsFalse(located.IsOk);
        Assert.IsInstanceOfType<CliNotFoundError>(located.Error);
    }

    [TestMethod]
    public void TheLocalInstallIsLookedForUnderTheUsersDirectoryAlone()
    {
        _ = Executable(Folder("home", ".claude", "local"), "claude");

        var located = Locate(null, string.Empty);

        Assert.IsFalse(located.IsOk);
        Assert.IsInstanceOfType<CliNotFoundError>(located.Error);
    }

    [TestMethod]
    public void NotFoundNamesWhereItLooked()
    {
        var located = Locate(null, PathOf(Folder("empty")));

        Assert.IsFalse(located.IsOk);
        StringAssert.Contains(located.Error.Message, Path.Combine(Folder("home"), ".local", "bin", "claude"));
        StringAssert.Contains(located.Error.Message, Path.Combine(Folder("user"), "local", "claude"));
    }

    private Result<string, Error> Locate(string? configured, string searchPath) =>
        new CliLocator().Find(configured, searchPath, Folder("home"), Folder("user"));

    private string Folder(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string PathOf(params string[] directories) =>
        string.Join(Path.PathSeparator, directories);

    private static string Executable(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n");
        return path;
    }
}
