using System.Reflection;
using System.Reflection.Emit;

namespace Claude.Agent.Tests.Architecture;

// The public surface, held by reflection over the built assembly: in which namespace, in what
// shape.
[TestClass]
public sealed class SurfaceTests
{
    // The raw-carrying members of the exported types, counted; RawMembers places each.
    private const int RawMemberCount = 42;

    private static readonly Assembly Library = typeof(ClaudeAgentClient).Assembly;
    private static readonly Type[] Exported = Library.GetExportedTypes();

    // The three clients and the run a call hands back: constructed by a caller or handed out by one,
    // mockable by the Azure rule — not sealed, a protected parameterless constructor, every public
    // method and property virtual.
    private static readonly Type[] Clients = [typeof(ClaudeAgentClient), typeof(ClaudeSessionClient), typeof(ClaudeAgentSession), typeof(ClaudeAgentRun)];

    // The input models a caller constructs with an object initialiser.
    private static readonly Type[] OptionsTypes =
    [
        typeof(ClaudeAgentClientOptions),
        typeof(ClaudeAgentSessionOptions),
        typeof(SessionQueryOptions),
        typeof(SandboxOptions),
        typeof(HookOutput),
    ];

    [TestMethod]
    public void EveryExportedTypeIsInClaudeAgentButTheRegistration()
    {
        foreach (var type in Exported)
        {
            var expected = type == typeof(Microsoft.Extensions.DependencyInjection.ClaudeAgentServiceCollectionExtensions)
                ? "Microsoft.Extensions.DependencyInjection"
                : "Claude.Agent";
            Assert.AreEqual(expected, type.Namespace, $"{type.FullName} is in the wrong namespace");
            Assert.IsFalse(type.IsNested, $"{type.FullName} is nested");
        }
    }

    [TestMethod]
    public void NoExportedTypeIsAnInterfaceAGenericOrAResult()
    {
        foreach (var type in Exported)
        {
            Assert.IsFalse(type.IsInterface, $"{type.Name} is an interface");
            Assert.IsFalse(type.IsGenericType, $"{type.Name} is generic");
            Assert.IsFalse(type.Name.StartsWith("Result", StringComparison.Ordinal) && !type.Name.StartsWith("ResultMessage", StringComparison.Ordinal), $"{type.Name} is a result type");
            Assert.IsFalse(type.Name.EndsWith("Error", StringComparison.Ordinal), $"{type.Name} is an error type");
            Assert.AreEqual(0, type.IsEnum ? 0 : type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length, $"{type.Name} has a public field");
        }
    }

    // What the rewrite took off the surface stays off it: the layers are folders and internal
    // types, and a live session is one object with methods.
    [TestMethod]
    public void NoExportedTypeIsNamedForALayer()
    {
        string[] gone = ["Handler", "Command", "Query", "Tracker", "Materializer", "Runtime", "Launcher", "Behaviour", "Behavior", "Pipeline", "Dto", "Parser", "Transport", "Codec"];
        foreach (var type in Exported)
        {
            Assert.IsFalse(gone.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal)), $"{type.Name} is named for a layer");
            Assert.IsTrue(!type.Name.EndsWith("Factory", StringComparison.Ordinal) || type == typeof(ClaudeAgentModelFactory), $"{type.Name} is a factory");
        }
    }

    // The client's options are how the binary is reached and nothing that is asked of it: the seven
    // members, and an argv builder that is not handed them.
    [TestMethod]
    public void TheClientOptionsHoldHowTheBinaryIsReachedAndArgvIsBuiltFromTheCallAlone()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(ClaudeAgentClientOptions.ConfigurationDirectory),
                nameof(ClaudeAgentClientOptions.Defaults),
                nameof(ClaudeAgentClientOptions.EnvironmentVariables),
                nameof(ClaudeAgentClientOptions.ExecutablePath),
                nameof(ClaudeAgentClientOptions.MaxMessageSize),
                nameof(ClaudeAgentClientOptions.ProcessGracePeriod),
                nameof(ClaudeAgentClientOptions.StandardError),
            },
            typeof(ClaudeAgentClientOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());

        Assert.AreEqual(typeof(ClaudeAgentSessionOptions), typeof(ClaudeAgentClientOptions).GetProperty(nameof(ClaudeAgentClientOptions.Defaults))!.PropertyType);

        var build = typeof(CommandLine).GetMethod(nameof(CommandLine.Build))!;
        CollectionAssert.DoesNotContain(
            build.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
            typeof(ClaudeAgentClientOptions));
    }

    // Both ways into a session take the per-call options, and they are optional on both.
    [TestMethod]
    public void QueryAsyncAndStartSessionAsyncTakeThePerCallOptions()
    {
        foreach (var name in new[] { nameof(ClaudeAgentClient.QueryAsync), nameof(ClaudeAgentClient.StartSessionAsync) })
        {
            var overloads = typeof(ClaudeAgentClient).GetMethods().Where(method => method.Name == name).ToArray();
            var taking = overloads.Count(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ClaudeAgentSessionOptions)));
            Assert.AreEqual(1, taking, name);
            Assert.AreEqual(2, overloads.Length, $"{name} has no overload that leaves the options out");
        }
    }

    [TestMethod]
    public void HooksAndThePermissionCallbackAreDelegatesOnTheOptions()
    {
        Assert.AreEqual(
            typeof(Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>>),
            typeof(ClaudeAgentSessionOptions).GetProperty(nameof(ClaudeAgentSessionOptions.PermissionCallback))!.PropertyType);

        var addHook = typeof(ClaudeAgentSessionOptions).GetMethods().Single(method => method.Name == nameof(ClaudeAgentSessionOptions.AddHook));
        CollectionAssert.AreEqual(
            new[] { typeof(HookEvent), typeof(string), typeof(Func<HookInput, CancellationToken, Task<HookOutput>>), typeof(TimeSpan?) },
            addHook.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

        Assert.AreEqual(0, Exported.Count(type => typeof(Delegate).IsAssignableFrom(type)), "the library declares a delegate type of its own");
    }

    [TestMethod]
    public void TheOptionsCarryNoPassThroughForFlags()
    {
        foreach (var property in OptionsTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.IsFalse(
                property.Name.Contains("Args", StringComparison.Ordinal) || property.Name.Contains("Flag", StringComparison.Ordinal) || property.Name is "User" or "Extra",
                $"{property.DeclaringType!.Name}.{property.Name} passes a flag through");
        }
    }

    // Where a message's frame sits: on AgentMessage, as JSON that may be null. That a read message
    // carries its line is held in Wire/FrameCodecDecodeTests.cs; that one the library assembled
    // without a frame carries none, in Sessions/RunReplayTests.cs and
    // Sessions/ResultShapeReplayTests.cs (the unanswered close).
    [TestMethod]
    public void EveryMessageHasItsFrameAsANullableJsonElementOnTheBaseType()
    {
        Assert.AreEqual(typeof(System.Text.Json.JsonElement?), typeof(AgentMessage).GetProperty(nameof(AgentMessage.Raw))!.PropertyType);
    }

    // The page's raw row: raw JSON is in an exported type in five places and no other, and the count
    // is pinned here. Every member below names the place it stands in; one that stands in none fails,
    // so raw cannot be added to the surface without a place for it.
    private static readonly Dictionary<string, RawPlace> RawMembers = new(StringComparer.Ordinal)
    {
        ["UnknownAdvisorToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownBashCodeExecutionToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownBrowserStateChange.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownCitation.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownCodeExecutionToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownContentBlock.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownDocumentSource.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownFallbackCreditStatus.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownHookInput.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownImageSource.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownMcpServerConfiguration.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownMessageOrigin.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownPermissionUpdate.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownStopHookBackgroundTask.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownTextEditorCodeExecutionToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownToolCaller.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownToolSearchToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownUsageIteration.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownWebFetchToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,
        ["UnknownWebSearchToolResultContent.Raw"] = RawPlace.TheUnknownOfASet,

        ["ToolUseBlock.RawInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["AssistantMessage.RawWireToolInputs"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["ServerToolUseBlock.RawInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["McpToolUseBlock.RawInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["DeferredToolUse.RawInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["ToolCall.RawInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["ToolCall.RawOutput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["UserMessage.RawToolOutput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["HookOutput.RawUpdatedInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["HookOutput.RawUpdatedToolOutput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["HookOutput.UpdatedMcpToolOutput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["ToolPermissionResult.RawUpdatedInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["PermissionDenial.RawInput"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["ElicitationHookInput.RequestedSchema"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["ElicitationResultHookInput.Content"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["RemoteTriggerCreateToolInput.Body"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["RemoteTriggerUpdateToolInput.Body"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["RemoteTriggerRunToolInput.Body"] = RawPlace.AToolsJsonOrAnMcpServers,
        ["RemoteTriggerCreateWebhookTriggerToolInput.Body"] = RawPlace.AToolsJsonOrAnMcpServers,

        ["StreamEventMessage.Event"] = RawPlace.TheStreamEventsBody,

        ["AgentMessage.Raw"] = RawPlace.TheLineAsTheBinaryWroteIt,

        ["ClaudeAgentSessionOptions.OutputSchema"] = RawPlace.TheOutputSchema,
    };

    // The five places of the page's raw row, in its order.
    private enum RawPlace
    {
        TheUnknownOfASet,
        AToolsJsonOrAnMcpServers,
        TheStreamEventsBody,
        TheLineAsTheBinaryWroteIt,
        TheOutputSchema,
    }

    [TestMethod]
    public void RawJsonIsInAnExportedTypeInFivePlacesAndNoOther()
    {
        var found = Exported
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => IsRaw(property.PropertyType))
                .Select(property => $"{type.Name}.{property.Name}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var unplaced = found.Where(member => !RawMembers.ContainsKey(member)).ToArray();
        Assert.AreEqual(0, unplaced.Length, $"raw in a place the page does not name: {string.Join(", ", unplaced)}");

        var gone = RawMembers.Keys.Where(member => !found.Contains(member, StringComparer.Ordinal)).ToArray();
        Assert.AreEqual(0, gone.Length, $"placed here and no longer on the surface: {string.Join(", ", gone)}");

        Assert.AreEqual(RawMemberCount, found.Length, string.Join(", ", found));

        CollectionAssert.AreEquivalent(
            Enum.GetValues<RawPlace>(),
            RawMembers.Values.Distinct().ToArray(),
            "a place the page names carries nothing");
    }

    // Raw is the wire's own JSON however it is held: the reader's element, a mutable node, and either
    // of those inside an array, a list or a map, which is how a member carrying one raw value per tool
    // call holds them.
    private static bool IsRaw(Type type) =>
        type == typeof(System.Text.Json.JsonElement)
        || typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(type)
        || type == typeof(System.Text.Json.JsonDocument)
        || (type.IsArray && IsRaw(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(IsRaw));

    [TestMethod]
    public void FramesDecodeThroughOneSourceGeneratedContextAndNoPolymorphismAttribute()
    {
        var contexts = Library.GetTypes().Where(type => type.IsSubclassOf(typeof(System.Text.Json.Serialization.JsonSerializerContext))).ToArray();
        CollectionAssert.AreEqual(new[] { typeof(FrameJsonContext) }, contexts);
        foreach (var type in Library.GetTypes())
        {
            Assert.AreEqual(0, type.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPolymorphicAttribute), inherit: false).Length, $"{type.Name} is [JsonPolymorphic]");
            Assert.AreEqual(0, type.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonDerivedTypeAttribute), inherit: false).Length, $"{type.Name} is [JsonDerivedType]");
        }
    }

    // kill(2) for the close sequence, and open, lseek, write and close for the transcript append,
    // which a FileStream would route through pwrite(2) and lose O_APPEND on.
    [TestMethod]
    public void FiveFunctionsAreCalledThroughLibcAndNoOther()
    {
        var imported = Library.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<System.Runtime.InteropServices.DllImportAttribute>())
            .OfType<System.Runtime.InteropServices.DllImportAttribute>()
            .Select(import => $"{import.Value}:{import.EntryPoint}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "libc:close", "libc:kill", "libc:lseek", "libc:open", "libc:write" }, imported);
    }

    [TestMethod]
    public void TheOneExceptionIsClaudeAgentException()
    {
        var exceptions = Exported.Where(type => typeof(Exception).IsAssignableFrom(type)).ToArray();
        CollectionAssert.AreEqual(new[] { typeof(ClaudeAgentException) }, exceptions);
        Assert.IsTrue(typeof(ClaudeAgentException).IsSealed);
        Assert.AreEqual(typeof(string), typeof(ClaudeAgentException).GetProperty(nameof(ClaudeAgentException.ErrorCode))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<AgentMessage>), typeof(ClaudeAgentException).GetProperty(nameof(ClaudeAgentException.Messages))!.PropertyType);
        Assert.AreEqual(typeof(ResultMessage), typeof(ClaudeAgentException).GetProperty(nameof(ClaudeAgentException.Result))!.PropertyType);
    }

    // The exception carries the messages so far, and its result is the last of them when that last
    // one is a result; a caller's test double builds one with them.
    [TestMethod]
    public void TheExceptionCarriesTheMessagesSoFarAndItsResultIsTheLastOfThemWhenThereIsOne()
    {
        var init = ClaudeAgentModelFactory.SystemInitMessage(sessionId: "s-1");
        var result = ClaudeAgentModelFactory.ResultMessage(subtype: "error_during_execution", isError: true, sessionId: "s-1");

        var ended = new ClaudeAgentException("the binary failed", ErrorCodes.Process, 1, [init, result]);
        CollectionAssert.AreEqual(new AgentMessage[] { init, result }, ended.Messages.ToArray());
        Assert.AreSame(result, ended.Result);

        var cut = new ClaudeAgentException("the binary died", ErrorCodes.Process, 1, [init]);
        Assert.IsNull(cut.Result);

        var bare = new ClaudeAgentException("refused");
        Assert.AreEqual(0, bare.Messages.Count);
        Assert.IsNull(bare.Result);
    }

    [TestMethod]
    public void EveryAsynchronousPublicMethodEndsInAsyncAndTakesTheTokenLastAndOptional()
    {
        var breaches = TokenRuleBreaches(PublicMethods()).ToArray();
        Assert.AreEqual(0, breaches.Length, string.Join("\n", breaches));
    }

    // The one method without the token is the one a type's IAsyncDisposable.DisposeAsync maps to. A
    // DisposeAsync of a type that is not IAsyncDisposable, and a public DisposeAsync beside an explicit
    // implementation of the interface, are not that method and are held to the token.
    [TestMethod]
    public void OnlyTheMethodIAsyncDisposableMapsToGoesWithoutTheToken()
    {
        Assert.AreEqual(0, TokenRuleBreaches(DeclaredPublicMethods(typeof(AsyncDisposal))).Count());
        CollectionAssert.AreEqual(
            new[] { "NotAsyncDisposal.DisposeAsync takes no CancellationToken" },
            TokenRuleBreaches(DeclaredPublicMethods(typeof(NotAsyncDisposal))).ToArray());
        CollectionAssert.AreEqual(
            new[] { "AsyncDisposalBeside.DisposeAsync takes no CancellationToken" },
            TokenRuleBreaches(DeclaredPublicMethods(typeof(AsyncDisposalBeside))).ToArray());
    }

    [TestMethod]
    public void NoSynchronousPublicMethodEndsInAsync()
    {
        foreach (var method in PublicMethods().Where(method => !IsAsynchronous(method.ReturnType)))
        {
            Assert.IsFalse(method.Name.EndsWith("Async", StringComparison.Ordinal), $"{method.DeclaringType!.Name}.{method.Name} ends in Async");
        }
    }

    [TestMethod]
    public void TheThreeClientsAndTheRunAreMockable()
    {
        foreach (var client in Clients)
        {
            Assert.IsFalse(client.IsSealed, $"{client.Name} is sealed");
            Assert.IsFalse(client.IsAbstract, $"{client.Name} is abstract");
            var mocking = client.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
            Assert.IsNotNull(mocking, $"{client.Name} has no protected parameterless constructor");
            Assert.IsTrue(mocking.IsFamily, $"{client.Name}'s parameterless constructor is not protected");
            foreach (var method in client.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName))
            {
                Assert.IsTrue(method.IsVirtual && !method.IsFinal, $"{client.Name}.{method.Name} is not virtual");
            }

            foreach (var property in client.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Assert.IsTrue(property.GetMethod!.IsVirtual, $"{client.Name}.{property.Name} is not virtual");
            }
        }
    }

    [TestMethod]
    public void EveryOptionsTypeIsSealedWithAPublicParameterlessConstructorAndNoRequiredMember()
    {
        foreach (var type in OptionsTypes)
        {
            Assert.IsTrue(type.IsSealed, $"{type.Name} is not sealed");
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), $"{type.Name} has no public parameterless constructor");
            Assert.IsNotNull(Activator.CreateInstance(type));
            Assert.AreEqual(0, type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), inherit: false).Length, $"{type.Name} has a required member");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(0, property.GetCustomAttributes(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), inherit: false).Length, $"{type.Name}.{property.Name} is required");
            }
        }
    }

    // The package ships its documentation, and nothing public is without it. The compiler holds the
    // whole of that — CS1591 is an error once the file is generated — and this holds that the file is
    // generated: it sits beside the assembly with a summary for every exported type, property and
    // enum member.
    [TestMethod]
    public void EveryExportedTypePropertyAndEnumMemberIsDocumentedInTheFileThePackageShips()
    {
        var file = Path.ChangeExtension(Library.Location, ".xml");
        Assert.IsTrue(File.Exists(file), $"{file} was not generated");
        var summaries = System.Xml.Linq.XDocument.Load(file).Descendants("member")
            .ToDictionary(member => member.Attribute("name")!.Value, member => member.Element("summary")?.Value.Trim() ?? string.Empty, StringComparer.Ordinal);

        var expected = new List<string>();
        foreach (var type in Exported)
        {
            expected.Add($"T:{type.FullName}");
            expected.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.GetIndexParameters().Length == 0 && property.Name != "EqualityContract")
                .Select(property => $"P:{type.FullName}.{property.Name}"));
            if (type.IsEnum)
            {
                expected.AddRange(Enum.GetNames(type).Select(name => $"F:{type.FullName}.{name}"));
            }
        }

        Assert.IsTrue(expected.Count > 250, $"{expected.Count} documented names expected: the scan has lost the surface");
        var undocumented = expected.Where(name => !summaries.TryGetValue(name, out var summary) || summary.Length == 0).ToArray();
        Assert.AreEqual(0, undocumented.Length, string.Join(", ", undocumented));
    }

    // The message stream is IAsyncEnumerable<AgentMessage> wherever one is handed out: the run the
    // two one-shot queries, the two asks and a session's send hand back, and what no run of a
    // session reads.
    [TestMethod]
    public void EveryMessageStreamIsAnAsyncEnumerableOfAgentMessageAndATurnsIsItsRun()
    {
        var streams = PublicMethods().Where(method => IsAsyncEnumerable(method.ReturnType)).ToArray();
        CollectionAssert.AreEqual(
            new[] { "ClaudeAgentClient.AskAsync", "ClaudeAgentClient.AskAsync", "ClaudeAgentClient.QueryAsync", "ClaudeAgentClient.QueryAsync", "ClaudeAgentSession.ReceiveMessagesAsync", "ClaudeAgentSession.SendAsync" },
            streams.Select(method => $"{method.DeclaringType!.Name}.{method.Name}").Order(StringComparer.Ordinal).ToArray());
        foreach (var stream in streams)
        {
            var expected = stream.Name == nameof(ClaudeAgentSession.ReceiveMessagesAsync) ? typeof(IAsyncEnumerable<AgentMessage>) : typeof(ClaudeAgentRun);
            Assert.AreEqual(expected, stream.ReturnType, $"{stream.DeclaringType!.Name}.{stream.Name}");
        }

        Assert.IsTrue(typeof(IAsyncEnumerable<AgentMessage>).IsAssignableFrom(typeof(ClaudeAgentRun)));
        Assert.AreEqual(typeof(Task<AgentTrace>), typeof(ClaudeAgentRun).GetMethod(nameof(ClaudeAgentRun.WaitForCompletionAsync))!.ReturnType);
        Assert.AreEqual(typeof(Task), typeof(ClaudeAgentRun).GetMethod(nameof(ClaudeAgentRun.InterruptAsync))!.ReturnType);
    }

    [TestMethod]
    public void TheLibraryReferencesOnlyTheRuntimeAndMicrosoftExtensions()
    {
        // The BCL is whatever sits beside System.Private.CoreLib in the shared framework.
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var reference in Library.GetReferencedAssemblies())
        {
            var name = reference.Name!;
            Assert.IsTrue(
                name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) || File.Exists(Path.Combine(runtime, name + ".dll")),
                $"the library references {name}");
        }
    }

    [TestMethod]
    public void OneTypeStartsAProcess()
    {
        var starters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in Library.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
            {
                if (Callees(method).Any(callee => callee.DeclaringType == typeof(System.Diagnostics.Process) && (callee.Name == nameof(System.Diagnostics.Process.Start) || callee is ConstructorInfo)))
                {
                    starters.Add(Outermost(type).Name);
                }
            }
        }

        CollectionAssert.AreEquivalent(new[] { "SubprocessCliTransport" }, starters.ToArray());
    }

    private static IEnumerable<MethodInfo> PublicMethods() => Exported.SelectMany(DeclaredPublicMethods);

    private static IEnumerable<MethodInfo> DeclaredPublicMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName);

    // The methods row's rule for an asynchronous method, as what breaks it: the name ends in Async, and
    // the last parameter is `CancellationToken cancellationToken = default`, but on the method a
    // type's IAsyncDisposable.DisposeAsync maps to, which the interface declares without one.
    private static IEnumerable<string> TokenRuleBreaches(IEnumerable<MethodInfo> methods)
    {
        foreach (var method in methods.Where(method => IsAsynchronous(method.ReturnType)))
        {
            var name = $"{method.DeclaringType!.Name}.{method.Name}";
            if (!method.Name.EndsWith("Async", StringComparison.Ordinal))
            {
                yield return $"{name} does not end in Async";
            }

            if (ImplementsAsyncDisposable(method))
            {
                continue;
            }

            var last = method.GetParameters().LastOrDefault();
            if (last is null)
            {
                yield return $"{name} takes no CancellationToken";
            }
            else if (last.ParameterType != typeof(CancellationToken))
            {
                yield return $"{name}'s last parameter is not the CancellationToken";
            }
            else
            {
                if (!last.HasDefaultValue)
                {
                    yield return $"{name}'s CancellationToken is not optional";
                }

                if (last.Name != "cancellationToken")
                {
                    yield return $"{name}'s CancellationToken is named {last.Name}";
                }
            }
        }
    }

    // The method is the one its type's IAsyncDisposable.DisposeAsync maps to, read off the type's
    // interface map.
    private static bool ImplementsAsyncDisposable(MethodInfo method) =>
        method.ReflectedType is { IsInterface: false } type
        && typeof(IAsyncDisposable).IsAssignableFrom(type)
        && type.GetInterfaceMap(typeof(IAsyncDisposable)).TargetMethods.Any(target => target.MethodHandle == method.MethodHandle);

    // A method is asynchronous when it returns a task or a type that is, or implements,
    // IAsyncEnumerable<>: the run a turn hands back counts.
    private static bool IsAsynchronous(Type type) =>
        type == typeof(Task) || type == typeof(ValueTask)
        || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        || IsAsyncEnumerable(type);

    private static bool IsAsyncEnumerable(Type type) =>
        (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
        || type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

    private static Type Outermost(Type type)
    {
        while (type.DeclaringType is { } outer)
        {
            type = outer;
        }

        return type;
    }

    // Every method a body calls, constructs or takes the address of, read off its IL.
    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            yield break;
        }

        var il = body.GetILAsByteArray()!;
        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            var opCode = il[position] == 0xFE ? TwoByteOpCodes[il[position + 1]] : OneByteOpCodes[il[position]];
            position += opCode.Size;
            var operandSize = opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
                _ => 4,
            };
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                MethodBase? callee;
                try
                {
                    callee = method.Module.ResolveMethod(BitConverter.ToInt32(il, position), typeArguments, methodArguments);
                }
                catch (ArgumentException)
                {
                    callee = null;
                }

                if (callee is not null)
                {
                    yield return callee;
                }
            }

            position += operandSize;
        }
    }

    private static readonly OpCode[] OneByteOpCodes = new OpCode[256];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[256];

    static SurfaceTests()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var opCode = (OpCode)field.GetValue(null)!;
            if (opCode.Size == 1)
            {
                OneByteOpCodes[opCode.Value] = opCode;
            }
            else
            {
                TwoByteOpCodes[opCode.Value & 0xFF] = opCode;
            }
        }
    }

    // IAsyncDisposable's DisposeAsync, implemented as a public method.
    private sealed class AsyncDisposal : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // A DisposeAsync on a type that is not IAsyncDisposable.
    private sealed class NotAsyncDisposal
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    // IAsyncDisposable implemented explicitly, with a public DisposeAsync of the same shape beside it
    // that the interface does not map to.
    private sealed class AsyncDisposalBeside : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }

        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }
}
