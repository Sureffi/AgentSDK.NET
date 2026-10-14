namespace Claude.Agent.Tests.Wire;

// ControlReply has a private protected constructor, which closes it to the world but not to this
// assembly: InternalsVisibleTo hands the test project the same access, so the record below compiles
// and reaches the default arm. The arm used to write a bare {} and say nothing, and a malformed reply
// would leave for the binary.
[TestClass]
public sealed class ReplyWriterTests
{
    [TestMethod]
    public void AnEmptyReplyIsTheBareObject()
    {
        var writer = new WireJson();

        ReplyWriter.Write(writer, EmptyReply.Instance);

        Assert.AreEqual("{}", writer.ToString());
    }

    // A hook that replies now and keeps running says so and says how long the binary waits for it.
    // The member is a TimeSpan, as every duration on this surface is, and the wire takes whole
    // milliseconds; an async reply says nothing else, whatever else is set on it.
    [TestMethod]
    public void AnAsyncHookReplySaysHowManyMillisecondsTheBinaryWaits()
    {
        var writer = new WireJson();

        ReplyWriter.WriteHookOutput(
            writer,
            new HookOutput { IsAsync = true, AsyncTimeout = TimeSpan.FromMilliseconds(1500), Continue = false },
            HookEvent.PreToolUse.ToString());

        Assert.AreEqual("""{"async": true, "asyncTimeout": 1500}""", writer.ToString());
    }

    [TestMethod]
    public void AnAsyncHookReplyWithNoTimeoutLeavesTheBinaryItsOwn()
    {
        var writer = new WireJson();

        ReplyWriter.WriteHookOutput(writer, new HookOutput { IsAsync = true }, HookEvent.PreToolUse.ToString());

        Assert.AreEqual("""{"async": true}""", writer.ToString());
    }

    [TestMethod]
    public void AReplyTheWriterDoesNotKnowThrowsNamingItsRuntimeType()
    {
        var thrown = Assert.ThrowsExactly<InvalidOperationException>(
            () => ReplyWriter.Write(new WireJson(), new RogueReply()));

        StringAssert.Contains(thrown.Message, typeof(RogueReply).ToString());
        StringAssert.Contains(thrown.Message, "is not a reply this writer knows");
    }

    private sealed record RogueReply : ControlReply;
}
