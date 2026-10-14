using System.Collections;
using System.Reflection;
using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Domain;

using static OptionsReflection;

[TestClass]
public sealed class OptionsTests
{
    private const string Uuid = "c3644dc2-4787-4013-8c4d-0d91a1b718ff";

    // Each a name the binary would not read back out of the rule SkillNames.Rule writes for it as that
    // skill alone.
    public static IEnumerable<object[]> RejectedSkillNames =>
    [
        [""],
        ["   "],
        ["\uFEFF"],
        ["blue\0print"],
        ["blue\uD800print"],
        ["blue) print"],
        ["blue),print"],
        ["*"],
        ["/blueprint"],
        ["plugin:*"],
        ["blueprint *"],
        [" blueprint"],
        ["blueprint "],
        ["blueprint\uFEFF"],
    ];

    // Each a name the binary reads back out of the rule SkillNames.Rule writes for it as that skill alone.
    public static IEnumerable<object[]> AcceptedSkillNames =>
    [
        ["blueprint"],
        ["mint:blueprint"],
        ["blue(print)"],
        ["blue,print"],
        ["blue print"],
        ["blue)print"],
        ["blue\\print"],
        ["blue\\\\print"],
        ["blueprint\\"],
        ["blueprint\\\\\\"],
        ["blue\\(print"],
        ["blue\\)print"],
        ["blue(print\\)"],
        ["blue\u0085"],
        ["blue*print"],
    ];

    [TestMethod]
    public void TheDefaultsValidate() => new ClaudeAgentClientOptions().Validate(TestHostEnvironments.Unmade);

    [TestMethod]
    public void ASessionIdThatIsNotAUuidIsRejected() =>
        Refused(new ClaudeAgentSessionOptions { SessionId = "not-a-uuid" }, nameof(ClaudeAgentSessionOptions.SessionId));

    [TestMethod]
    public void ASessionIdAlongsideResumeIsRejectedUnlessTheSessionIsForked() =>
        Refused(
            new ClaudeAgentSessionOptions { SessionId = Uuid, ResumeSessionId = "older" },
            nameof(ClaudeAgentSessionOptions.SessionId));

    [TestMethod]
    public void ASessionIdAlongsideResumeIsAcceptedWhenTheSessionIsForked() =>
        new ClaudeAgentSessionOptions { SessionId = Uuid, ResumeSessionId = "older", Fork = true }.Validate();

    [TestMethod]
    public void ContinueLatestTogetherWithResumeIsRejected() =>
        Refused(
            new ClaudeAgentSessionOptions { ContinueLatest = true, ResumeSessionId = "older" },
            nameof(ClaudeAgentSessionOptions.ResumeSessionId));

    [TestMethod]
    public void ARelativeWorkingDirectoryIsRejected() =>
        Refused(
            new ClaudeAgentSessionOptions { WorkingDirectory = "repos/AgentSDK.NET" },
            nameof(ClaudeAgentSessionOptions.WorkingDirectory));

    [TestMethod]
    public void ARootedWorkingDirectoryIsAccepted() =>
        new ClaudeAgentSessionOptions { WorkingDirectory = "/tmp/fixcwd" }.Validate();

    [TestMethod]
    public void ABlankExecutablePathIsRejected() =>
        Refused(new ClaudeAgentClientOptions { ExecutablePath = " " }, nameof(ClaudeAgentClientOptions.ExecutablePath));

    [TestMethod]
    public void ANegativeMaxTurnsIsRejected() =>
        Refused(new ClaudeAgentSessionOptions { MaxTurns = -1 }, nameof(ClaudeAgentSessionOptions.MaxTurns));

    [TestMethod]
    public void ZeroMaxTurnsIsAccepted() => new ClaudeAgentSessionOptions { MaxTurns = 0 }.Validate();

    [TestMethod]
    public void AMaxBudgetOfZeroIsRejected() =>
        Refused(new ClaudeAgentSessionOptions { MaxBudgetUsd = 0m }, nameof(ClaudeAgentSessionOptions.MaxBudgetUsd));

    [TestMethod]
    public void ANegativeTaskBudgetIsRejected() =>
        Refused(new ClaudeAgentSessionOptions { TaskBudget = -1 }, nameof(ClaudeAgentSessionOptions.TaskBudget));

    [TestMethod]
    public void ZeroTaskBudgetIsAccepted() => new ClaudeAgentSessionOptions { TaskBudget = 0 }.Validate();

    [TestMethod]
    public void AZeroProcessGracePeriodIsRejected() =>
        Refused(
            new ClaudeAgentClientOptions { ProcessGracePeriod = TimeSpan.Zero },
            nameof(ClaudeAgentClientOptions.ProcessGracePeriod));

    [TestMethod]
    public void AZeroMaxMessageSizeIsRejected() =>
        Refused(new ClaudeAgentClientOptions { MaxMessageSize = 0 }, nameof(ClaudeAgentClientOptions.MaxMessageSize));

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void ABlankConfigurationDirectoryIsRejected(string directory) =>
        Refused(new ClaudeAgentClientOptions { ConfigurationDirectory = directory }, nameof(ClaudeAgentClientOptions.ConfigurationDirectory));

    [TestMethod]
    [DataRow("CLAUDE_CONFIG_DIR")]
    [DataRow("CLAUDE_SECURESTORAGE_CONFIG_DIR")]
    public void AVariableTheClientSetsIsRejectedAmongTheEnvironmentVariables(string name)
    {
        var options = new ClaudeAgentClientOptions();
        options.EnvironmentVariables[name] = "/somewhere";

        Refused(options, nameof(ClaudeAgentClientOptions.EnvironmentVariables));
    }

    // The user's directory is CLAUDE_CONFIG_DIR as the host inherited it, else ~/.claude, and no
    // spelling of it is the client's: a trailing separator or a relative path is the same directory.
    // Where the inherited CLAUDE_CONFIG_DIR is the configuration directory, the user's is the inherited
    // secure-storage directory, else ~/.claude, and that one is refused.
    [TestMethod]
    public void TheUsersOwnDirectoryIsRejectedAsTheConfigurationDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        var inherited = Path.Combine(root, "inherited");
        var user = TestHostEnvironments.UserDirectory(root);
        var profile = TestHostEnvironments.Under(root);
        var configured = TestHostEnvironments.Under(root, new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = inherited });
        var inheritedAsUser = TestHostEnvironments.Under(root, new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = user });
        var inheritedAsStorage = TestHostEnvironments.Under(root, new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = inherited,
            ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = inherited,
        });

        foreach (var (directory, host) in new[]
        {
            (user, profile),
            (user + Path.DirectorySeparatorChar, profile),
            (Path.GetRelativePath(Environment.CurrentDirectory, user), profile),
            (user, inheritedAsUser),
            (inherited, inheritedAsStorage),
            (inherited + Path.DirectorySeparatorChar, inheritedAsStorage),
        })
        {
            var options = new ClaudeAgentClientOptions { ConfigurationDirectory = directory };

            var refused = Assert.ThrowsExactly<ArgumentException>(() => options.Validate(host), directory);
            var constructed = Assert.ThrowsExactly<ArgumentException>(() => new ClaudeAgentClient(options, null, new FixtureTransportFactory(new FrameCodec()), host), directory);

            Assert.AreEqual(nameof(ClaudeAgentClientOptions.ConfigurationDirectory), refused.ParamName);
            Assert.AreEqual(nameof(ClaudeAgentClientOptions.ConfigurationDirectory), constructed.ParamName);
        }

        new ClaudeAgentClientOptions { ConfigurationDirectory = user }.Validate(configured);
        Assert.IsFalse(Directory.Exists(root));
    }

    // The directory the login is read from is never a home either: the one an inherited
    // CLAUDE_SECURESTORAGE_CONFIG_DIR names, and ~/.claude when it is inherited set to nothing, though
    // the user's directory is another — the host's own CLAUDE_CONFIG_DIR, or an outer SDK session's
    // with ~/.claude's login handed down beside it. Without the inherited variable the same directory is
    // a home.
    [TestMethod]
    public void TheDirectoryTheLoginIsReadFromIsRejectedAsTheConfigurationDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        var user = TestHostEnvironments.UserDirectory(root);
        var storage = Path.Combine(root, "storage");
        var elsewhere = Path.Combine(root, "elsewhere");
        var outer = Path.Combine(root, "outer");
        var inheritedStorage = TestHostEnvironments.Under(root, new Dictionary<string, string> { ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = storage });
        var inheritedBoth = TestHostEnvironments.Under(root, new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = elsewhere,
            ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = storage,
        });
        var nested = TestHostEnvironments.Under(root, new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = outer,
            ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = user,
        });
        var setToNothing = TestHostEnvironments.Under(root, new Dictionary<string, string>
        {
            ["CLAUDE_CONFIG_DIR"] = elsewhere,
            ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = "",
        });

        foreach (var (directory, host, loginDirectory) in new[]
        {
            (storage, inheritedStorage, storage),
            (storage + Path.DirectorySeparatorChar, inheritedStorage, storage),
            (Path.GetRelativePath(Environment.CurrentDirectory, storage), inheritedStorage, storage),
            (storage, inheritedBoth, storage),
            (user, nested, user),
            (user, setToNothing, user),
        })
        {
            var options = new ClaudeAgentClientOptions { ConfigurationDirectory = directory };

            var refused = Assert.ThrowsExactly<ArgumentException>(() => options.Validate(host), directory);
            var constructed = Assert.ThrowsExactly<ArgumentException>(() => new ClaudeAgentClient(options, null, new FixtureTransportFactory(new FrameCodec()), host), directory);

            Assert.AreEqual(nameof(ClaudeAgentClientOptions.ConfigurationDirectory), refused.ParamName, directory);
            Assert.AreEqual(nameof(ClaudeAgentClientOptions.ConfigurationDirectory), constructed.ParamName, directory);
            Assert.AreEqual(
                $"The configuration directory {Path.GetFullPath(directory)} is the one the user's login is read from; a client's sessions keep a directory of their own. (Parameter 'ConfigurationDirectory')",
                refused.Message,
                directory);
            Assert.AreEqual(refused.Message, constructed.Message, directory);
            Assert.AreEqual(loginDirectory, ConfigurationDirectories.Resolve(directory, host).SecureStorage, directory);
        }

        new ClaudeAgentClientOptions { ConfigurationDirectory = storage }.Validate(TestHostEnvironments.Under(root));
        new ClaudeAgentClientOptions { ConfigurationDirectory = user }.Validate(TestHostEnvironments.Under(root, new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = outer }));
        Assert.IsFalse(Directory.Exists(root));
    }

    // The directories are compared as they resolve on disk: a symbolic link to the user's directory is
    // the user's directory, and so is the directory the user's directory links to. A directory beside
    // them is not.
    [TestMethod]
    public void TheUsersOwnDirectoryThroughASymbolicLinkIsRejectedAsTheConfigurationDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        try
        {
            var user = TestHostEnvironments.UserDirectory(root);
            Directory.CreateDirectory(user);
            var link = Path.Combine(root, "link");
            Directory.CreateSymbolicLink(link, user);
            var target = Path.Combine(root, "target");
            Directory.CreateDirectory(target);
            var linked = Path.Combine(root, "linked");
            var linkedUserDirectory = TestHostEnvironments.UserDirectory(linked);
            Directory.CreateDirectory(Path.GetDirectoryName(linkedUserDirectory)!);
            Directory.CreateSymbolicLink(linkedUserDirectory, target);
            var profile = TestHostEnvironments.Under(root);
            var linkedUser = TestHostEnvironments.Under(linked);

            foreach (var (directory, host) in new[]
            {
                (link, profile),
                (link + Path.DirectorySeparatorChar, profile),
                (target, linkedUser),
                (target + Path.DirectorySeparatorChar, linkedUser),
            })
            {
                var options = new ClaudeAgentClientOptions { ConfigurationDirectory = directory };

                var refused = Assert.ThrowsExactly<ArgumentException>(() => options.Validate(host), directory);

                Assert.AreEqual(nameof(ClaudeAgentClientOptions.ConfigurationDirectory), refused.ParamName);
            }

            new ClaudeAgentClientOptions { ConfigurationDirectory = target }.Validate(profile);
            new ClaudeAgentClientOptions { ConfigurationDirectory = link }.Validate(linkedUser);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // A link among the ancestors is followed too, as where /home is a link to /var/home: the user's
    // directory spelled through the linked parent is the user's directory spelled physically, either
    // way round. A directory beside it under the same parent is not.
    [TestMethod]
    public void TheUsersOwnDirectoryThroughALinkedParentIsRejectedAsTheConfigurationDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        try
        {
            var physical = Path.Combine(root, "physical");
            var linked = Path.Combine(root, "linked");
            Directory.CreateDirectory(TestHostEnvironments.UserDirectory(physical));
            Directory.CreateSymbolicLink(linked, physical);

            foreach (var (directory, host) in new[]
            {
                (TestHostEnvironments.UserDirectory(physical), TestHostEnvironments.Under(linked)),
                (TestHostEnvironments.UserDirectory(linked), TestHostEnvironments.Under(physical)),
            })
            {
                var options = new ClaudeAgentClientOptions { ConfigurationDirectory = directory };

                var refused = Assert.ThrowsExactly<ArgumentException>(() => options.Validate(host), directory);

                Assert.AreEqual(nameof(ClaudeAgentClientOptions.ConfigurationDirectory), refused.ParamName);
            }

            new ClaudeAgentClientOptions { ConfigurationDirectory = TestHostEnvironments.DefaultConfigurationDirectory(physical) }
                .Validate(TestHostEnvironments.Under(linked));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // A host a session of an SDK client started inherits CLAUDE_CONFIG_DIR as that client's
    // configuration directory. Where it is this client's, the default or one named, by the comparison
    // the refusal makes — a trailing separator, a symbolic link, an ancestor's among them — it is not
    // the user's: the client is made, and the user's directory and the login's are the inherited
    // secure-storage directory, else ~/.claude, as they are when it is inherited set to nothing; the
    // child is told the inherited value, else the variable set to nothing. One that names another
    // directory stays the user's.
    [TestMethod]
    public void AnInheritedConfigurationDirectoryThatIsTheClientsOwnIsNotTheUsers()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        try
        {
            var home = TestHostEnvironments.DefaultConfigurationDirectory(root);
            var user = TestHostEnvironments.UserDirectory(root);
            var storage = Path.Combine(root, "storage");
            var named = Path.Combine(root, "named");
            var link = Path.Combine(root, "link");
            var linkedProfile = Path.Combine(root, "linked-profile");
            Directory.CreateDirectory(home);
            Directory.CreateSymbolicLink(link, home);
            Directory.CreateSymbolicLink(linkedProfile, Path.Combine(root, "profile"));

            foreach (var (configured, inherited) in new (string?, string)[]
            {
                (null, home),
                (null, home + Path.DirectorySeparatorChar),
                (null, link),
                (null, Path.Combine(linkedProfile, ".config", "claude-agent")),
                (named, named),
            })
            {
                foreach (var (secureStorage, expected, told) in new (string?, string, string)[] { (null, user, ""), ("", user, ""), (storage, storage, storage) })
                {
                    var variables = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = inherited };
                    if (secureStorage is not null)
                    {
                        variables["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = secureStorage;
                    }

                    var client = new ClaudeAgentClient(
                        new ClaudeAgentClientOptions { ConfigurationDirectory = configured },
                        null,
                        new FixtureTransportFactory(new FrameCodec()),
                        TestHostEnvironments.Under(root, variables));

                    var label = $"{inherited} with {secureStorage ?? "no secure-storage directory"}";
                    Assert.AreEqual(configured ?? home, client.Directories.Configuration, label);
                    Assert.AreEqual(expected, client.Directories.User, label);
                    Assert.AreEqual(expected, client.Directories.SecureStorage, label);
                    Assert.AreEqual(told, client.Directories.SecureStorageValue, label);
                }
            }

            var elsewhere = Path.Combine(root, "elsewhere");
            var outer = new ClaudeAgentClient(
                new ClaudeAgentClientOptions(),
                null,
                new FixtureTransportFactory(new FrameCodec()),
                TestHostEnvironments.Under(root, new Dictionary<string, string>
                {
                    ["CLAUDE_CONFIG_DIR"] = elsewhere,
                    ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = storage,
                }));

            Assert.AreEqual(elsewhere, outer.Directories.User);
            Assert.AreEqual(storage, outer.Directories.SecureStorage);
            Assert.AreEqual(storage, outer.Directories.SecureStorageValue);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // ~/.config/claude-agent: .config/claude-agent under the user's profile, and no other folder read.
    [TestMethod]
    public void TheDefaultConfigurationDirectoryIsDotConfigClaudeAgentInTheUsersProfileAndTheLoginIsTheUsers()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        var options = new ClaudeAgentClientOptions();

        var client = new ClaudeAgentClient(options, null, new FixtureTransportFactory(new FrameCodec()), TestHostEnvironments.Under(root));

        Assert.AreEqual(Path.Combine(root, "profile", ".config", "claude-agent"), client.Options.ConfigurationDirectory);
        Assert.AreEqual(TestHostEnvironments.DefaultConfigurationDirectory(root), client.Directories.Configuration);
        Assert.AreEqual(TestHostEnvironments.UserDirectory(root), client.Directories.User);
        Assert.AreEqual(TestHostEnvironments.UserDirectory(root), client.Directories.SecureStorage);
        Assert.AreEqual(string.Empty, client.Directories.SecureStorageValue);
        Assert.IsNull(options.ConfigurationDirectory);
        Assert.IsFalse(Directory.Exists(root), "making a client made a directory");
    }

    [TestMethod]
    public void AConfigurationDirectoryIsTheFullPathItResolvesToWhenTheClientIsMade()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        var options = new ClaudeAgentClientOptions { ConfigurationDirectory = Path.Combine("relative", "..", "home") };

        var client = new ClaudeAgentClient(options, null, new FixtureTransportFactory(new FrameCodec()), TestHostEnvironments.Under(root));

        Assert.AreEqual(Path.Combine(Environment.CurrentDirectory, "home"), client.Options.ConfigurationDirectory);
        Assert.AreEqual(Path.Combine("relative", "..", "home"), options.ConfigurationDirectory);
    }

    // An inherited CLAUDE_CONFIG_DIR set to nothing is not set: the user's directory is ~/.claude. An
    // inherited CLAUDE_SECURESTORAGE_CONFIG_DIR set to nothing is inherited: the binary reads the login
    // from ~/.claude under it, whatever CLAUDE_CONFIG_DIR says, and the child is told it set to nothing.
    [TestMethod]
    public void AnInheritedConfigurationDirectorySetToNothingIsNotSetAndASecureStorageDirectorySetToNothingNamesDotClaude()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-options-" + Guid.NewGuid().ToString("n"));
        var user = TestHostEnvironments.UserDirectory(root);
        var elsewhere = Path.Combine(root, "elsewhere");

        foreach (var (configuration, expected) in new[] { ("", user), (elsewhere, elsewhere) })
        {
            var host = TestHostEnvironments.Under(root, new Dictionary<string, string>
            {
                ["CLAUDE_CONFIG_DIR"] = configuration,
                ["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = "",
            });

            var client = new ClaudeAgentClient(new ClaudeAgentClientOptions(), null, new FixtureTransportFactory(new FrameCodec()), host);

            Assert.AreEqual(expected, client.Directories.User, configuration);
            Assert.AreEqual(user, client.Directories.SecureStorage, configuration);
            Assert.AreEqual(string.Empty, client.Directories.SecureStorageValue, configuration);
        }
    }

    [TestMethod]
    public void ASystemPromptFileWithNoPathIsRefusedWhereItIsBuilt() =>
        Assert.ThrowsExactly<ArgumentException>(() => AgentSystemPrompt.FromFile(" "));

    // A closed set is open to a value the library has not named, and closed against no value at
    // all: the default struct names nothing, so there is nothing to send, and Validate says which
    // member it was.
    [TestMethod]
    public void ADefaultPermissionModeIsRejectedNamingTheMember() =>
        Refused(
            new ClaudeAgentSessionOptions { PermissionMode = default(PermissionMode) },
            nameof(ClaudeAgentSessionOptions.PermissionMode));

    [TestMethod]
    public void ADefaultEffortIsRejectedNamingTheMember() =>
        Refused(new ClaudeAgentSessionOptions { Effort = default(Effort) }, nameof(ClaudeAgentSessionOptions.Effort));

    [TestMethod]
    public void ADefaultSettingSourceIsRejectedNamingTheMember() =>
        Refused(
            new ClaudeAgentSessionOptions { SettingSources = [SettingSource.User, default] },
            nameof(ClaudeAgentSessionOptions.SettingSources));

    [TestMethod]
    public void ADefaultMemoryScopeOnASubagentIsRejectedNamingTheAgents()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Agents["bee"] = new AgentDefinition("a bee", "buzz") { Memory = default(AgentMemory) };

        Refused(options, nameof(ClaudeAgentSessionOptions.Agents));
    }

    [TestMethod]
    public void ADefaultEffortOnASubagentIsRejectedNamingTheAgents()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Agents["bee"] = new AgentDefinition("a bee", "buzz") { Effort = default(Effort) };

        Refused(options, nameof(ClaudeAgentSessionOptions.Agents));
    }

    [TestMethod]
    public void ADefaultPermissionModeOnASubagentIsRejectedNamingTheAgents()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Agents["bee"] = new AgentDefinition("a bee", "buzz") { PermissionMode = default(PermissionMode) };

        Refused(options, nameof(ClaudeAgentSessionOptions.Agents));
    }

    // The one closed set a caller hands the binary outside the options: a hook's decision. The
    // default names nothing, so there would be nothing for the binary to read.
    [TestMethod]
    public void ADefaultDecisionOnAHooksOutputIsRefusedWhereItIsSet()
    {
        var refused = Assert.ThrowsExactly<ArgumentException>(
            () => new HookOutput { Decision = default(PermissionDecision) });

        Assert.AreEqual("value", refused.ParamName);
    }

    [TestMethod]
    public void ADecisionTheLibraryDoesNotNameIsAcceptedOnAHooksOutput() =>
        Assert.AreEqual("ponder", new HookOutput { Decision = "ponder" }.Decision.ToString());

    // A mode the library does not name is a caller's to send; only the empty one is refused.
    [TestMethod]
    public void APermissionModeTheLibraryDoesNotNameIsAccepted() =>
        new ClaudeAgentSessionOptions { PermissionMode = "ponderMode" }.Validate();

    [TestMethod]
    public void ADefaultPermissionModeIsRefusedWhereTheSessionSetsIt()
    {
        var refused = Assert.ThrowsExactly<ArgumentException>(
            () => new Session().SetPermissionModeAsync(default));

        Assert.AreEqual("mode", refused.ParamName);
    }

    [TestMethod]
    public void ADefaultHookEventIsRefusedWhereTheHookIsRegistered()
    {
        var refused = Assert.ThrowsExactly<ArgumentException>(
            () => new ClaudeAgentSessionOptions().AddHook(
                default,
                null,
                (_, _) => Task.FromResult(new HookOutput())));

        Assert.AreEqual("hookEvent", refused.ParamName);
    }

    [TestMethod]
    public void ADefaultClosedSetIsRefusedWhereAPermissionUpdateIsBuilt()
    {
        RefusedUpdate("behavior", () => PermissionUpdate.AddRules(["Read(*)"], default, PermissionUpdateDestination.Session));
        RefusedUpdate("behavior", () => PermissionUpdate.ReplaceRules(["Read(*)"], default, PermissionUpdateDestination.Session));
        RefusedUpdate("behavior", () => PermissionUpdate.RemoveRules(["Read(*)"], default, PermissionUpdateDestination.Session));
        RefusedUpdate("mode", () => PermissionUpdate.SetMode(default, PermissionUpdateDestination.Session));
        RefusedUpdate("destination", () => PermissionUpdate.SetMode(PermissionMode.Default, default(PermissionUpdateDestination)));
        RefusedUpdate("destination", () => PermissionUpdate.AddDirectories(["."], default(PermissionUpdateDestination)));
        RefusedUpdate("destination", () => PermissionUpdate.RemoveDirectories(["."], default(PermissionUpdateDestination)));
    }

    // A value the library has not named builds an update as the caller spelt it.
    [TestMethod]
    public void APermissionUpdateKeepsTheBehaviorAndDestinationTheLibraryDoesNotName()
    {
        var update = PermissionUpdate.AddRules(["Read(*)"], "escalate", "workspaceSettings");

        Assert.AreEqual("escalate", update.Behavior.ToString());
        Assert.AreEqual("workspaceSettings", update.Destination?.ToString());
    }

    private static void RefusedUpdate(string member, Func<PermissionUpdate> build)
    {
        var refused = Assert.ThrowsExactly<ArgumentException>(() => build(), $"{member} was accepted empty");

        Assert.AreEqual(member, refused.ParamName);
    }

    [TestMethod]
    public void ANegativeThinkingBudgetIsRejected() =>
        Refused(
            new ClaudeAgentSessionOptions { Thinking = ThinkingOptions.WithBudget(-1) },
            nameof(ClaudeAgentSessionOptions.Thinking));

    [TestMethod]
    public void APermissionCallbackTogetherWithAPermissionPromptToolIsRejected() =>
        Refused(
            new ClaudeAgentSessionOptions
            {
                PermissionPromptTool = "mcp__auth__ask",
                PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Allow()),
            },
            nameof(ClaudeAgentSessionOptions.PermissionPromptTool));

    [TestMethod]
    [DynamicData(nameof(RejectedSkillNames))]
    public void ASkillNameThatCanNeverMatchIsRejected(string name)
    {
        var options = new ClaudeAgentSessionOptions();
        options.Skills.Add(name);

        var refused = Assert.ThrowsExactly<ArgumentException>(options.Validate, $"'{name}' was accepted");
        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.Skills), refused.ParamName);
    }

    [TestMethod]
    [DynamicData(nameof(AcceptedSkillNames))]
    public void ASkillNameTheBinaryReadsBackWholeIsAccepted(string name)
    {
        var options = new ClaudeAgentSessionOptions();
        options.Skills.Add(name);

        options.Validate();
    }

    [TestMethod]
    [DynamicData(nameof(AcceptedSkillNames))]
    public void TheRuleWrittenForAnAcceptedSkillNameIsReadBackAsThatNameAlone(string name)
    {
        var read = AllowedToolsReading.Read(SkillNames.Rule(name));

        Assert.AreEqual(1, read.Count);
        Assert.AreEqual("Skill", read[0].Tool);
        Assert.AreEqual(name, read[0].Content);
    }

    [TestMethod]
    public void ARefusedSkillNameSaysWhatTheBinaryWouldReadInstead()
    {
        StringAssert.Contains(SkillNames.Problem("/blueprint"), "as 'blueprint'");
        StringAssert.Contains(SkillNames.Problem("mint:*"), "starts with 'mint'");
        StringAssert.Contains(SkillNames.Problem("*"), "AllSkills");
    }

    [TestMethod]
    public void EverySkillIsAcceptedAsAnOption() => new ClaudeAgentSessionOptions { AllSkills = true }.Validate();

    [TestMethod]
    public void EverySkillTogetherWithNamedSkillsIsRejected()
    {
        var options = new ClaudeAgentSessionOptions { AllSkills = true };
        options.Skills.Add("blueprint");

        Refused(options, nameof(ClaudeAgentSessionOptions.Skills));
    }

    [TestMethod]
    public void TheSkillNameCheckIsTheOneValidateRuns() => Assert.IsNotNull(SkillNames.Problem("*"));

    [TestMethod]
    public void ABlankAdditionalDirectoryIsRejected()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AdditionalDirectories.Add(" ");

        Refused(options, nameof(ClaudeAgentSessionOptions.AdditionalDirectories));
    }

    [TestMethod]
    public void ABlankPluginDirectoryIsRejected()
    {
        var options = new ClaudeAgentSessionOptions();
        options.PluginDirectories.Add(" ");

        Refused(options, nameof(ClaudeAgentSessionOptions.PluginDirectories));
    }

    [TestMethod]
    public void AStdioServerWithABlankCommandIsRefusedWhereItIsBuilt() =>
        Assert.ThrowsExactly<ArgumentException>(() => McpServerConfiguration.Stdio("  "));

    [TestMethod]
    public void AnMcpServerNameWithSurroundingSpaceIsRejected()
    {
        var options = new ClaudeAgentSessionOptions();
        options.McpServers[" mint"] = McpServerConfiguration.Stdio("mint-server");

        Refused(options, nameof(ClaudeAgentSessionOptions.McpServers));
    }

    [TestMethod]
    public void AnAgentWithBothAnEffortLevelAndATokenCountIsRejected()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Agents["bee"] = new AgentDefinition("a bee", "buzz") { Effort = Effort.Low, EffortTokens = 100 };

        Refused(options, nameof(ClaudeAgentSessionOptions.Agents));
    }

    // A server configuration is shared between the defaults and every call, so it holds nothing the
    // caller can still move: the environment and the headers are its own, in the order given.
    [TestMethod]
    public void AnMcpServerKeepsItsOwnEnvironmentAndHeaders()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["HIVE_TOKEN"] = "dev", ["A"] = "1" };
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer dev" };
        var stdio = McpServerConfiguration.Stdio("hive-server", ["--stdio"], environment);
        var sse = McpServerConfiguration.Sse(new Uri("https://mint.example/sse"), headers);
        var http = McpServerConfiguration.Http(new Uri("https://mint.example/mcp"), headers);

        environment["HIVE_TOKEN"] = "prod";
        environment.Remove("A");
        headers["Authorization"] = "Bearer prod";

        CollectionAssert.AreEqual(new[] { "HIVE_TOKEN", "A" }, stdio.EnvironmentVariables!.Keys.ToArray());
        Assert.AreEqual("dev", stdio.EnvironmentVariables["HIVE_TOKEN"]);
        Assert.AreEqual("Bearer dev", sse.Headers!["Authorization"]);
        Assert.AreEqual("Bearer dev", http.Headers!["Authorization"]);
    }

    [TestMethod]
    public void CloneRoundTripsEveryPublicPropertyAndTheHooks()
    {
        var options = new ClaudeAgentSessionOptions();
        var properties = Properties(typeof(ClaudeAgentSessionOptions));
        Assert.IsTrue(properties.Length > 35, $"only {properties.Length} properties were reflected");

        foreach (var property in properties)
        {
            Distinguish(options, property);
        }

        options.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));

        var clone = options.Clone();

        foreach (var property in properties)
        {
            AssertCarried(property.GetValue(options), property.GetValue(clone), property.Name);
        }

        CollectionAssert.AreEqual(options.Hooks.ToArray(), clone.Hooks.ToArray(), "Hooks did not survive Clone");
    }

    // The client's own options carry the defaults inside them, so their clone is held to the same:
    // every property of both, and the defaults as a copy.
    [TestMethod]
    public void TheClientOptionsCloneRoundTripsEveryPublicPropertyAndTheDefaults()
    {
        var options = new ClaudeAgentClientOptions();
        var properties = Properties(typeof(ClaudeAgentClientOptions));
        Assert.AreEqual(7, properties.Length, string.Join(", ", properties.Select(property => property.Name)));

        foreach (var property in properties)
        {
            Distinguish(options, property);
        }

        var clone = options.Clone();

        foreach (var property in properties)
        {
            AssertCarried(property.GetValue(options), property.GetValue(clone), property.Name);
        }
    }

    [TestMethod]
    public void CloneGivesTheCopyItsOwnCollections()
    {
        var options = new ClaudeAgentClientOptions();
        options.Defaults.AllowedTools.Add("Read");
        options.EnvironmentVariables["A"] = "1";
        options.Defaults.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));

        var clone = options.Clone();
        clone.Defaults.AllowedTools.Add("Write");
        clone.EnvironmentVariables["B"] = "2";
        clone.Defaults.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));

        CollectionAssert.AreEqual(new[] { "Read" }, options.Defaults.AllowedTools.ToArray());
        Assert.AreEqual(1, options.EnvironmentVariables.Count);
        Assert.AreEqual(1, options.Defaults.Hooks.Count);
    }

    // The client keeps a snapshot. Sandbox and an agent definition are the two mutable objects the
    // options hold, so the snapshot has its own: a change after the client is built would otherwise
    // reach the next session without Validate having seen it.
    [TestMethod]
    public void CloneGivesTheCopyItsOwnSandboxAndItsOwnAgents()
    {
        var options = new ClaudeAgentSessionOptions { Sandbox = new SandboxOptions { Enabled = true, AllowedDomains = ["hive.example"] } };
        options.Agents["bee"] = new AgentDefinition("a bee", "buzz") { Effort = Effort.Low, Tools = ["Read"] };

        var clone = options.Clone();
        options.Sandbox.Enabled = false;
        options.Sandbox.AllowedDomains.Add("telemetry.example");
        options.Agents["bee"].EffortTokens = 1;
        options.Agents["bee"].Tools!.Add("Bash");

        Assert.IsTrue(clone.Sandbox!.Enabled);
        CollectionAssert.AreEqual(new[] { "hive.example" }, clone.Sandbox.AllowedDomains!.ToArray());
        Assert.IsNull(clone.Agents["bee"].EffortTokens);
        CollectionAssert.AreEqual(new[] { "Read" }, clone.Agents["bee"].Tools!.ToArray());
        clone.Validate();
    }

    // A JsonElement is copied where it is handed in, so the document it was parsed from can go.
    [TestMethod]
    public void AJsonElementHandedInOutlivesTheDocumentItCameFrom()
    {
        var options = new ClaudeAgentSessionOptions();
        var output = new HookOutput();
        ToolPermissionResult allowed;
        using (var document = JsonDocument.Parse("""{"type":"object"}"""))
        {
            options.OutputSchema = document.RootElement;
            output.RawUpdatedInput = document.RootElement;
            output.RawUpdatedToolOutput = document.RootElement;
            output.UpdatedMcpToolOutput = document.RootElement;
            allowed = ToolPermissionResult.Allow(rawUpdatedInput: document.RootElement);
        }

        const string Expected = """{"type":"object"}""";
        Assert.AreEqual(Expected, options.OutputSchema!.Value.GetRawText());
        Assert.AreEqual(Expected, options.Clone().OutputSchema!.Value.GetRawText());
        Assert.AreEqual(Expected, output.RawUpdatedInput!.Value.GetRawText());
        Assert.AreEqual(Expected, output.RawUpdatedToolOutput!.Value.GetRawText());
        Assert.AreEqual(Expected, output.UpdatedMcpToolOutput!.Value.GetRawText());
        Assert.AreEqual(Expected, allowed.RawUpdatedInput!.Value.GetRawText());
    }

    // An MCP status reply hands back an in-process server's configuration with no object behind it:
    // this process never registered one, so there would be nothing to serve its tools from. It is
    // refused before a process starts, as the two other kinds this library cannot send are.
    [TestMethod]
    public void AnInProcessServerReadOffTheWireIsRefusedWhereItIsSentBackOut()
    {
        var read = McpServerConfigurationParser.Read(JsonDocument.Parse("""{"type":"sdk","name":"calc"}""").RootElement);
        Assert.IsInstanceOfType<McpSdkServerConfiguration>(read, out var sdk);
        Assert.IsNull(sdk.Tools);

        var options = new ClaudeAgentSessionOptions();
        options.McpServers["calc"] = sdk;

        var refused = Refused(options, nameof(ClaudeAgentSessionOptions.McpServers));
        StringAssert.Contains(refused.Message, "AddClaudeTools");
    }

    private static ArgumentException Refused(ClaudeAgentClientOptions options, string member)
    {
        var refused = Assert.ThrowsExactly<ArgumentException>(() => options.Validate(TestHostEnvironments.Unmade));
        Assert.AreEqual(member, refused.ParamName);
        return refused;
    }

    private static ArgumentException Refused(ClaudeAgentSessionOptions options, string member)
    {
        var refused = Assert.ThrowsExactly<ArgumentException>(options.Validate);
        Assert.AreEqual(member, refused.ParamName);
        return refused;
    }

    // A caller's test double, as the clients' protected parameterless constructor is there for.
    private sealed class Session : ClaudeAgentSession;
}
