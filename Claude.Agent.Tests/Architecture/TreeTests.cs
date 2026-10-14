using System.Text.RegularExpressions;
using System.Xml.Linq;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Architecture;

// What the tree holds and does not hold, read off the working copy the tests were built from.
[TestClass]
public sealed partial class TreeTests
{
    internal static readonly string Root = FindRoot();

    private static readonly string[] SourceExtensions = [".cs", ".md", ".csproj", ".props", ".slnx", ".json", ".editorconfig"];

    [TestMethod]
    public void NothingInTheTreeComesFromOrNamesAnotherSdk()
    {
        // Spelt in two halves so this file passes its own check.
        var needle = "pyt" + "hon";
        Assert.IsFalse(File.Exists(Path.Combine(Root, ".gitmodules")), "the tree has a submodule");
        foreach (var file in Files())
        {
            Assert.AreNotEqual(".py", Path.GetExtension(file), $"{Relative(file)} is a script from another runtime");
            if (SourceExtensions.Contains(Path.GetExtension(file)) && !Relative(file).StartsWith("Claude.Agent.Tests/fixtures/transcripts/", StringComparison.Ordinal))
            {
                var text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains(needle, StringComparison.OrdinalIgnoreCase), $"{Relative(file)} names it");
                // Its private functions are underscore-led and snake-cased; nothing of this tree's is.
                Assert.IsFalse(ForeignName().IsMatch(text), $"{Relative(file)} cites {ForeignName().Match(text).Value.Trim()}");
            }
        }
    }

    // The phrases and names of another SDK's that were found in the tree and rewritten as this SDK's
    // own — its messages for a binary it could not find, start, write to or run, for a session it
    // could not find, for a skill name, a permission callback, a control request and a command line;
    // its warnings; its install locations and helpers; the two environment variables only it reads —
    // and its strip set, which holds the C0 separators: none is back in any source. It holds these and
    // no others; what else was copied is found by reading, not by this list.
    [TestMethod]
    public void NoPhraseOrNameFoundCopiedFromAnotherSdkIsBackInTheTree()
    {
        // Each spelt in two halves, as above.
        string[] needles =
        [
            "Claude Code " + "not found",
            "@anthropic-ai/" + "claude-code",
            "Failed to start " + "Claude Code",
            "Refusing to execute " + "batch script",
            "not found in " + "project directory",
            "not found (no " + "projects directory)",
            ">= '" + "\\u001c'",
            "Skill names must be " + "non-empty strings",
            "contains a surrogate " + "code point",
            "leading or trailing whitespace " + "can never match",
            "byte-order marks " + "are not allowed",
            "wildcard-suffix names " + "are not allowed",
            "may not start " + "with '/'",
            "consecutive backslashes " + "are not allowed",
            "may not end with an " + "unpaired backslash",
            "option to enable " + "every skill",
            "HasDelimiter" + "OrControlChar",
            "CanUseTool" + "Shadowing",
            "will not be " + "invoked",
            "auto-" + "approves",
            "before the callback " + "is consulted",
            "To gate every " + "tool call",
            "can also shadow " + "the callback",
            ".npm-" + "global",
            "\".ya" + "rn\"",
            "IsNative" + "Executable",
            "IsBatch" + "Script",
            "path" + "Hit",
            "ProcessTransport is " + "not ready",
            "Cannot write to " + "terminated process",
            "Cannot write to process " + "that exited",
            "Failed to write to " + "process stdin",
            "Dropping truncated " + "JSON",
            "Skipping non-JSON " + "line",
            "Command failed " + "with exit code",
            "Working directory " + "does not exist:",
            "exceeded maximum " + "buffer size",
            "No hook callback " + "found for ID",
            "Unsupported control " + "request subtype",
            "canUseTool callback " + "is not provided",
            "cannot be used with " + "PermissionPromptTool",
            "unsafe to pass on a " + "Windows command line",
            "is unsupported in the " + "Agent SDK",
            "Minimum required " + "version is",
            "Some features may " + "not work correctly",
            "CLAUDE_AGENT_SDK_" + "SKIP_VERSION_CHECK",
            "CLAUDE_CODE_" + "STREAM_CLOSE_TIMEOUT",
            "Skills" + "Defaults",
        ];
        var sources = Files()
            .Where(file => SourceExtensions.Contains(Path.GetExtension(file)))
            .Where(file => !Relative(file).StartsWith("Claude.Agent.Tests/fixtures/transcripts/", StringComparison.Ordinal))
            .ToArray();
        Assert.IsTrue(
            sources.Count(file => Relative(file).StartsWith("Claude.Agent/", StringComparison.Ordinal)) > 100,
            "the scan has lost the library");
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (var needle in needles)
            {
                Assert.IsFalse(text.Contains(needle, StringComparison.OrdinalIgnoreCase), $"{Relative(file)} holds {needle}");
            }
        }
    }

    // No API key path: what ships and what runs beside it never names the key, its header or the
    // API's host. The login the binary holds is the only credential.
    [TestMethod]
    public void NothingThatShipsNamesAnApiKeyItsHeaderOrTheApisHost()
    {
        // Each spelt in two halves, as above.
        string[] needles = ["ANTHROPIC_" + "API_KEY", "ANTHROPIC_" + "AUTH_TOKEN", "x-api" + "-key", "api.anthropic" + ".com"];
        var shipped = Files()
            .Where(file => Path.GetExtension(file) == ".cs")
            .Where(file => new[] { "Claude.Agent/", "Claude.Agent.Recorder/", "samples/" }.Any(prefix => Relative(file).StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        Assert.IsTrue(shipped.Length > 100, $"{shipped.Length} sources found: the scan has lost the library");
        foreach (var file in shipped)
        {
            var text = File.ReadAllText(file);
            foreach (var needle in needles)
            {
                Assert.IsFalse(text.Contains(needle, StringComparison.OrdinalIgnoreCase), $"{Relative(file)} names {needle}");
            }
        }
    }

    [TestMethod]
    public void TheLibraryTakesMicrosoftExtensionsPackagesAndNothingElse()
    {
        foreach (var package in Packages("Claude.Agent/Claude.Agent.csproj"))
        {
            Assert.IsTrue(package.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal), $"the library references {package}");
        }
    }

    [TestMethod]
    public void NoProjectInTheTreeTakesAnAnthropicPackageOrAnotherProvider()
    {
        foreach (var project in Files().Where(file => Path.GetExtension(file) == ".csproj"))
        {
            foreach (var package in Packages(Relative(project)))
            {
                Assert.IsTrue(
                    package.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal),
                    $"{Relative(project)} references {package}");
            }
        }
    }

    // `dotnet pack` at the root makes one package.
    [TestMethod]
    public void OnlyTheLibraryPacks()
    {
        foreach (var project in Files().Where(file => Path.GetExtension(file) == ".csproj" && Relative(file) != "Claude.Agent/Claude.Agent.csproj"))
        {
            var packable = XDocument.Load(project).Descendants("IsPackable").Select(element => element.Value).SingleOrDefault();
            Assert.AreEqual("false", packable, $"{Relative(project)} packs");
        }
    }

    // The sample opens the library with one using, and the registration with the container's own.
    [TestMethod]
    public void TheSampleOpensTheLibraryWithOneUsing()
    {
        var usings = Directory.EnumerateFiles(Path.Combine(Root, "samples/Mint"), "*.cs")
            .SelectMany(File.ReadLines)
            .Where(line => line.StartsWith("using ", StringComparison.Ordinal))
            .Select(line => line["using ".Length..].TrimEnd(';'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "Claude.Agent" }, usings.Where(name => name.StartsWith("Claude", StringComparison.Ordinal)).ToArray());
        CollectionAssert.Contains(usings, "Microsoft.Extensions.DependencyInjection");
    }

    [TestMethod]
    public void TheTestProjectIsMSTest()
    {
        var sdk = XDocument.Load(Path.Combine(Root, "Claude.Agent.Tests/Claude.Agent.Tests.csproj")).Root!.Attribute("Sdk")!.Value;
        Assert.IsTrue(sdk.StartsWith("MSTest.Sdk/", StringComparison.Ordinal), sdk);
    }

    // Every string a parser branches on, and every value a closed-set struct names, is founded four
    // ways, none above another: a value in a recorded stdout, a value in a transcript the binary wrote
    // and the fixtures keep, a name in the fixtures' list of what the reference declares, or a word or
    // a phrase quoted whole in the fixtures' README's section on what the binary did not emit, which
    // says how its absence was established (NamedWhereAbsenceIsEstablished). A branch is a type key
    // the codec dispatches on, a `case "x":`, a `"x" or "y" =>` arm, an `is "x" or "y"` test, an
    // `== "x"` or `!= "x"` test, the same against a const naming "x", and a string table a parser
    // checks membership of. No transcript parser reads a line by a literal prefix: each line is
    // parsed whole. The parsers are every *Parser.cs under Infrastructure/Parsing, ToolTypes, which
    // reads a tool's JSON by the tool's name, and the four that read a transcript's lines. A named
    // value is read off the struct itself, so a set a later lane adds is founded here without a line
    // being added. The stdout and the transcripts are the recorded channels: the stdin and the argv
    // are what this library itself wrote, and a value founded on them would be founded on nothing but
    // the claim under test. What only goes out — an effort level, a settings source, a subagent's
    // memory scope — stands on the declared list.
    [TestMethod]
    public void EveryParserBranchIsFoundedByARecordedStdoutOrTranscriptTheDeclaredListOrTheFixturesReadme()
    {
        var transcripts = Directory.EnumerateFiles(Path.Combine(Fixture.Root, "transcripts"), "*.jsonl", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        Assert.IsTrue(transcripts.Length >= 27, $"{transcripts.Length} transcripts read: the fixtures have lost them");
        var recorded = string.Concat(
            Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl").Select(File.ReadAllText).Concat(transcripts));
        var named = NamedWhereAbsenceIsEstablished(File.ReadAllText(Path.Combine(Root, "Claude.Agent.Tests/fixtures/README.md")));
        var declared = File.ReadLines(Path.Combine(Root, "Claude.Agent.Tests/fixtures/wire-names.tsv"))
            .Select(line => line.Split('\t'))
            .Where(cells => cells.Length == 2)
            .Select(cells => cells[1])
            .ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(declared.Count > 500, $"{declared.Count} declared names read: the list is lost");
        var branches = typeof(FrameTypes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(field => (Where: nameof(FrameTypes), Branch: (string)field.GetRawConstantValue()!))
            .ToList();
        var parsing = Path.Combine(Root, "Claude.Agent/Infrastructure/Parsing");
        string[] transcriptParsers =
        [
            "Claude.Agent/Application/Transcripts/SessionReader.cs",
            "Claude.Agent/Domain/Transcripts/SessionFork.cs",
            "Claude.Agent/Domain/Transcripts/TranscriptChain.cs",
            "Claude.Agent/Domain/Transcripts/TranscriptScan.cs",
        ];
        var parsers = Directory.EnumerateFiles(parsing, "*Parser.cs", SearchOption.AllDirectories)
            .Append(Path.Combine(parsing, "ToolTypes.cs"))
            .Concat(transcriptParsers.Select(file => Path.Combine(Root, file)))
            .ToArray();
        CollectionAssert.IsSubsetOf(
            new[] { "ContentBlockParser.cs", "MessageParser.cs", "UsageParser.cs", "ToolTypes.cs" },
            parsers.Select(Path.GetFileName).ToArray());
        var prefixes = new List<(string Where, string Prefix)>();
        foreach (var file in parsers)
        {
            var source = File.ReadAllText(file);
            var where = Path.GetFileName(file);
            branches.AddRange(CaseLabel().Matches(source).Select(match => (where, match.Groups[1].Value)));
            branches.AddRange(Equality().Matches(source).Select(match => (where, match.Groups[1].Value)));
            branches.AddRange(Constant().Matches(source)
                .Where(constant => Regex.IsMatch(source, $@"(?:[!=]=|\bcase|\bis)\s+{constant.Groups[1].Value}\b"))
                .Select(constant => (where, constant.Groups[2].Value)));
            foreach (var pattern in new[] { SwitchArm(), IsTest(), StringTable() })
            {
                branches.AddRange(pattern.Matches(source).SelectMany(found => Literal().Matches(found.Groups[1].Value)).Select(match => (where, match.Groups[1].Value)));
            }

            prefixes.AddRange(LinePrefix().Matches(source).Select(match => (where, match.Groups[1].Value.Replace("\\\"", "\"", StringComparison.Ordinal))));
        }

        Assert.AreEqual(0, prefixes.Count, string.Join("; ", prefixes.Select(found => $"{found.Where}: {found.Prefix}")));

        var sets = 0;
        foreach (var type in ClosedSets.Structs)
        {
            sets++;
            branches.AddRange(ClosedSets.Named(type).Select(value => (type.Name, value.Value.ToString()!)));
        }

        Assert.IsTrue(branches.Count >= 761, $"{branches.Count} branches found, fewer than the 761 the scan finds today: it has lost a parser or a closed set");
        Assert.IsTrue(sets >= 121, $"{sets} closed sets reached: the scan has lost them");
        var unfounded = branches.Distinct()
            .Where(found => !recorded.Contains($":\"{found.Branch}\"", StringComparison.Ordinal)
                && !recorded.Contains($":\\\"{found.Branch}\\\"", StringComparison.Ordinal)
                && !declared.Contains(found.Branch)
                && !named.Contains(found.Branch))
            .Select(found => $"{found.Where}: {found.Branch}")
            .ToArray();

        Assert.AreEqual(0, unfounded.Length, string.Join("; ", unfounded));
    }

    [TestMethod]
    public void TheNotEmittedSectionFoundsAWordItCarries()
    {
        var named = NamedWhereAbsenceIsEstablished(ReadmeWithAnAbsence);

        foreach (var word in new[] { "hook_started", "hook_response", "remote_agent", "in_process_teammate", "auto_mode_scan" })
        {
            Assert.IsTrue(named.Contains(word), word);
        }
    }

    [TestMethod]
    public void TheNotEmittedSectionFoundsNoPartOfALongerWord()
    {
        var named = NamedWhereAbsenceIsEstablished(ReadmeWithAnAbsence);

        foreach (var part in new[] { "started", "response", "remote", "agent", "process", "team", "mate", "mode_scan" })
        {
            Assert.IsFalse(named.Contains(part), part);
        }
    }

    [TestMethod]
    public void TheNotEmittedSectionFoundsAPhraseQuotedWhole()
    {
        var named = NamedWhereAbsenceIsEstablished(ReadmeWithAnAbsence);

        foreach (var phrase in new[] { "cloud session", "auto-mode scan", "MCP task", "Event:matcher" })
        {
            Assert.IsTrue(named.Contains(phrase), phrase);
        }
    }

    // A word of the section is founded wherever it stands, inside a quotation too; a part of a quoted
    // phrase that is not one word is founded by nothing.
    [TestMethod]
    public void TheNotEmittedSectionFoundsNoPartOfAQuotedPhraseThatIsNotOneWord()
    {
        var named = NamedWhereAbsenceIsEstablished(ReadmeWithAnAbsence);

        foreach (var part in new[] { "auto-mode", "mode scan", "Event:", "MCP task`, auto_mode_scan \"auto-mode scan" })
        {
            Assert.IsFalse(named.Contains(part), part);
        }

        foreach (var word in new[] { "auto", "mode", "scan", "Event", "matcher", "MCP", "task" })
        {
            Assert.IsTrue(named.Contains(word), word);
        }
    }

    [TestMethod]
    public void AWordOutsideTheNotEmittedSectionFoundsNothing()
    {
        var named = NamedWhereAbsenceIsEstablished(ReadmeWithAnAbsence);

        foreach (var outside in new[] { "hook_callback", "local_agent", "subagent", "permission_denials", "denied" })
        {
            Assert.IsFalse(named.Contains(outside), outside);
        }

        var moved = NamedWhereAbsenceIsEstablished(ReadmeWithAnAbsence.Replace("remote_agent", "permission_denials", StringComparison.Ordinal));
        Assert.IsTrue(moved.Contains("permission_denials"));
        Assert.IsFalse(moved.Contains("remote_agent"));
    }

    // A README laid out as the fixtures' is, a section ahead of the one that establishes absences and
    // one after it, each carrying words and phrases of its own.
    private const string ReadmeWithAnAbsence = """
        # fixtures

        hook_callback is how an SDK hook is called; a task of kind local_agent is labelled "subagent".

        ## what the binary did not emit, and how that was established

            hook_started, hook_response   not for an SDK callback hook: no event names one, and
                                          hook_name is "Event:matcher" for a settings hook
            a task's type                 remote_agent "cloud session", in_process_teammate
                                          `MCP task`, auto_mode_scan "auto-mode scan"

        ## what each fixture holds

            denied               permission_denials on the result
        """;

    // What the fixtures README founds, read off the one section that establishes absences: the one
    // headed "what the binary did not emit, and how that was established", from the line after its
    // heading to the next heading of its level or above. The README's other words are prose and
    // fixture names, and a branch founded on those would be founded on the README mentioning anything.
    // A value is founded as one of the section's words, a run of word characters with none on either
    // side, so `auto` is founded by neither `automatic` nor `auto_mode_scan`; or as the whole of what
    // one pair of backticks or of double quotes holds on one line, which is how a value that is not one
    // word is founded, and never by a part of a longer quotation.
    internal static HashSet<string> NamedWhereAbsenceIsEstablished(string readme)
    {
        const string Title = "what the binary did not emit, and how that was established";
        var lines = readme.Split('\n');
        var start = Array.FindIndex(lines, line => MarkdownHeading().Match(line) is { Success: true } heading && heading.Groups[2].Value == Title);
        Assert.IsTrue(start >= 0, $"the fixtures README has no section headed \"{Title}\"");
        var level = MarkdownHeading().Match(lines[start]).Groups[1].Length;
        var end = Array.FindIndex(lines, start + 1, line => MarkdownHeading().Match(line) is { Success: true } heading && heading.Groups[1].Length <= level);
        var section = string.Join('\n', lines[(start + 1)..(end < 0 ? lines.Length : end)]);
        return Word().Matches(section).Select(word => word.Value)
            .Concat(QuotedPhrase().Matches(section).Select(phrase => phrase.Groups[1].Success ? phrase.Groups[1].Value : phrase.Groups[2].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    // Two things in this tree start the real binary: the recorder, on every fixture it records, and
    // the sample. Both name the model, and the model is haiku — the cheapest one that answers — so
    // neither spends the account's usage on a larger one. Every model name either file writes is
    // read here, whether it goes out at a start or at a verb.
    [TestMethod]
    public void WhateverStartsTheRealBinaryNamesHaikuAndNoOtherModel()
    {
        string[] starters = ["Claude.Agent.Recorder/Fixtures.cs", "samples/Mint/Program.cs"];
        var named = 0;
        foreach (var starter in starters)
        {
            foreach (var match in ModelNamed().Matches(File.ReadAllText(Path.Combine(Root, starter))).Cast<Match>())
            {
                named++;
                Assert.AreEqual("haiku", match.Groups[1].Value, $"{starter} names another model");
            }
        }

        Assert.AreEqual(starters.Length, named, "a starter names no model, or one has been added that this test does not read");
    }

    [TestMethod]
    public void EveryFixtureIsThreeFilesAndTheRecorderNamesIt()
    {
        var recorder = File.ReadAllText(Path.Combine(Root, "Claude.Agent.Recorder/Fixtures.cs"));
        var names = Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl").Select(file => Path.GetFileName(file)[..^".stdout.jsonl".Length]).ToArray();
        Assert.AreNotEqual(0, names.Length);
        foreach (var name in names)
        {
            Assert.IsTrue(File.Exists(Path.Combine(Fixture.Root, $"{name}.stdin.jsonl")), $"{name} has no stdin");
            Assert.IsTrue(File.Exists(Path.Combine(Fixture.Root, $"{name}.argv.json")), $"{name} has no argv");
            Assert.IsTrue(recorder.Contains($"[\"{name}\"]", StringComparison.Ordinal), $"the recorder has no fixture named {name}");
        }
    }

    [TestMethod]
    public void NoFixtureCarriesTheMachineOrTheAccountThatRecordedIt()
    {
        foreach (var file in Directory.EnumerateFiles(Fixture.Root, "*", SearchOption.AllDirectories))
        {
            Assert.IsNull(Carried(File.ReadAllText(file)), $"{Relative(file)} {Carried(File.ReadAllText(file))}");
        }
    }

    // The recorder's scrub leaves nothing the check above refuses, in every spelling a recording has
    // been seen to carry or could: an address outside any key, in a sentence, of someone else's, or
    // right after an escape, which it leaves whole; the email key escaped, spaced, or holding no
    // address; an organization and an atis token inside a string inside a string, and values holding
    // escapes of their own at three depths. Every string that holds JSON still parses. What it does
    // to a fixture already on disk is nothing.
    [TestMethod]
    public void TheRecordersScrubLeavesNothingTheFixtureCheckRefusesAndChangesNoFixture()
    {
        string[] recorded =
        [
            """{"type":"system","cwd":"/home/recorder/cwd","userEmail":"The user's email address is ada@lovelace.org."}""",
            """{"account":{"email":"ada@lovelace.org","organization":"ada@lovelace.org's Organization"}}""",
            """{"account": {"email": "ada@lovelace.org", "organization": "Analytical Engines"}}""",
            """{"account":{"email":"not given"}}""",
            """{"content":"{\"email\":\"ada@lovelace.org\",\"organization\":\"Analytical Engines\",\"atis\":\"tok-1\"}"}""",
            """{"content":"{\"entry\":\"{\\\"organization\\\":\\\"Analytical Engines\\\",\\\"atis\\\":\\\"tok-2\\\"}\"}"}""",
            """{"type":"atis-latch","atis":"tok-3"}""",
            """{"text":"Co-Authored-By: Someone <noreply@anthropic.com>, and git@github.com:owner/repository.git"}""",
            """{"text":"line one\nada@lovelace.org, then\tbob@babbage.org, then \u00e4ada@lovelace.org"}""",
            """{"content":"{\"text\":\"one\\nada@lovelace.org\"}"}""",
            """{"account":{"email":"ada\u0040lovelace.org"}}""",
            """{"account":{"organization":"Analytical \"Engines\"","atis":"tok-4\\"}}""",
            """{"content":"{\"organization\":\"Analytical \\\"Engines\\\"\\n\",\"atis\":\"tok-5\\\\\\\"x\"}"}""",
            """{"content":"{\"entry\":\"{\\\"organization\\\":\\\"Analytical \\\\\\\"Engines\\\\\\\"\\\\n\\\",\\\"atis\\\":\\\"tok-5\\\\\\\\\\\\\\\"x\\\"}\"}"}""",
        ];

        foreach (var line in recorded)
        {
            var scrubbed = Claude.Agent.Recorder.FixtureScrub.Apply(line, "/home/recorder");
            Assert.IsNull(Carried(scrubbed), $"{Carried(scrubbed)}: {scrubbed}");
            Assert.IsFalse(scrubbed.Contains("Analytical Engines", StringComparison.Ordinal), scrubbed);
            Assert.IsFalse(scrubbed.Contains("tok-", StringComparison.Ordinal), scrubbed);
            Assert.IsFalse(scrubbed.Contains("Engines", StringComparison.Ordinal), scrubbed);
            Assert.IsFalse(scrubbed.Contains("lovelace", StringComparison.Ordinal), scrubbed);
            Assert.IsFalse(scrubbed.Contains("babbage", StringComparison.Ordinal), scrubbed);
            ParsesWhole(scrubbed);
        }

        var files = Directory.EnumerateFiles(Fixture.Root, "*", SearchOption.AllDirectories).ToArray();
        Assert.IsTrue(files.Length > 100, $"{files.Length} fixture files found: the scan has lost them");
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.AreEqual(text, Claude.Agent.Recorder.FixtureScrub.Apply(text, "/home/recorder"), $"the scrub changes {Relative(file)}");
        }
    }

    // A JSON text parses, and so does every string in it that holds JSON, however deep.
    private static void ParsesWhole(string json)
    {
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        var pending = new Stack<System.Text.Json.JsonElement>([parsed.RootElement]);
        while (pending.TryPop(out var element))
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        pending.Push(property.Value);
                    }

                    break;
                case System.Text.Json.JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        pending.Push(item);
                    }

                    break;
                case System.Text.Json.JsonValueKind.String when element.GetString() is ['{', ..] nested:
                    ParsesWhole(nested);
                    break;
                default:
                    break;
            }
        }
    }

    // What of the machine or the account a fixture's text carries, said as the failure; null for none.
    private static string? Carried(string text)
    {
        foreach (Match home in HomeDirectory().Matches(text))
        {
            if (home.Value != "/home/user")
            {
                return $"names a home directory, {home.Value}";
            }
        }

        foreach (var email in Claude.Agent.Recorder.FixtureScrub.Values(text, "email"))
        {
            if (email.Length != 0 && email != "user@example.com")
            {
                return $"names an account, {email}";
            }
        }

        foreach (Match address in Address().Matches(text))
        {
            if (address.Value != "user@example.com")
            {
                return $"carries an address, {address.Value}";
            }
        }

        foreach (var organization in Claude.Agent.Recorder.FixtureScrub.Values(text, "organization"))
        {
            if (organization != "user@example.com's Organization")
            {
                return $"names an organization, {organization}";
            }
        }

        foreach (var token in Claude.Agent.Recorder.FixtureScrub.Values(text, "atis"))
        {
            if (token.Length != 0)
            {
                return "carries an atis token";
            }
        }

        return null;
    }

    private static IEnumerable<string> Packages(string project) =>
        XDocument.Load(Path.Combine(Root, project)).Descendants("PackageReference").Select(reference => reference.Attribute("Include")!.Value);

    private static IEnumerable<string> Files() =>
        Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Where(file => !Relative(file).Split('/').Any(part => part is "bin" or "obj" or ".git" or ".vs" or ".idea"));

    private static string Relative(string file) => Path.GetRelativePath(Root, file).Replace('\\', '/');

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Claude.Agent.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Claude.Agent.slnx is not above the test output.");
    }

    [GeneratedRegex("case \"([^\"]+)\"")]
    private static partial Regex CaseLabel();

    [GeneratedRegex("^\\s*((?:\"[^\"]+\"(?:\\s+or\\s+)?)+)\\s*=>", RegexOptions.Multiline)]
    private static partial Regex SwitchArm();

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex Literal();

    [GeneratedRegex("\\bis\\s+((?:\"[^\"]+\"(?:\\s+or\\s+)?)+)")]
    private static partial Regex IsTest();

    // A model named to be sent: an assignment or a call taking it as a literal.
    [GeneratedRegex("Model\\s*=\\s*\"([^\"]+)\"|SetModelAsync\\(\"([^\"]+)\"")]
    private static partial Regex ModelNamed();

    [GeneratedRegex("(?:string\\[\\]|ImmutableHashSet<string>)\\s+\\w+\\s*=\\s*\\[([^\\]]+)\\]")]
    private static partial Regex StringTable();

    [GeneratedRegex("[!=]=\\s*\"([^\"]+)\"")]
    private static partial Regex Equality();

    // A value a parser names once and compares by name: `const string X = "x"` with `== X`, `case X`
    // or `is X` in the same file.
    [GeneratedRegex("const\\s+string\\s+(\\w+)\\s*=\\s*\"([^\"\\\\]+)\"\\s*;")]
    private static partial Regex Constant();

    [GeneratedRegex("StartsWith\\(\"((?:[^\"\\\\]|\\\\.)+)\"")]
    private static partial Regex LinePrefix();

    // A markdown heading: its #s, which are its level, and its text.
    [GeneratedRegex("^(#{1,6})[ \\t]+(.*?)[ \\t]*\\r?$")]
    private static partial Regex MarkdownHeading();

    // A run of word characters, each match taking the run to its end, so no word is read out of a
    // longer one.
    [GeneratedRegex("\\w+")]
    private static partial Regex Word();

    // Read left to right, so a quote inside a pair of backticks is part of what they hold, and the
    // reverse; the pair holds at least one character and never a line break.
    [GeneratedRegex("`([^`\\n]+)`|\"([^\"\\n]+)\"")]
    private static partial Regex QuotedPhrase();

    [GeneratedRegex("/home/[A-Za-z0-9_.-]+")]
    private static partial Regex HomeDirectory();

    // An address starts at no character an escape holds, as the scrub reads one.
    [GeneratedRegex("(?<!\\\\(?:u[0-9A-Fa-f]{0,3})?)[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\\.[A-Za-z0-9-]+)*\\.[A-Za-z]{2,}")]
    private static partial Regex Address();

    [GeneratedRegex("(?<![A-Za-z0-9_])_[a-z]+(?:_[a-z0-9]+)+")]
    private static partial Regex ForeignName();
}
